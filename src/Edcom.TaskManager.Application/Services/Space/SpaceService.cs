using Edcom.TaskManager.Application.Services.Space.Contracts;
using Edcom.TaskManager.Domain.Enums;
using Edcom.TaskManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SpaceEntity = Edcom.TaskManager.Domain.Entities.Space;

namespace Edcom.TaskManager.Application.Services.Space;

public class SpaceService(AppDbContext dbContext) : ISpaceService
{
    public async Task<Result<PagedList<SpaceResponse>>> GetAllByOrgAsync(long orgId, long callerUserId, SpaceFilterRequest filter, CancellationToken ct)
    {
        if (!await IsMemberAsync(orgId, callerUserId, ct)) return SpaceErrors.Forbidden;

        var query = dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.OrganizationId == orgId && !s.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = $"%{filter.Search}%";
            query = query.Where(x => EF.Functions.ILike(x.Name, s) || EF.Functions.ILike(x.Slug, s));
        }

        if (filter.BoardType.HasValue)
            query = query.Where(x => x.BoardType == filter.BoardType.Value);

        if (filter.IsActive.HasValue)
            query = query.Where(x => x.IsActive == filter.IsActive.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.Name)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(x => new SpaceResponse
            {
                Id              = x.Id,
                OrganizationId  = x.OrganizationId,
                Name            = x.Name,
                Slug            = x.Slug,
                BoardType       = x.BoardType,
                IssueKeyPrefix  = x.IssueKeyPrefix,
                IssueCounter    = x.IssueCounter,
                IssueCount      = x.Tickets.Count(t => !t.IsDeleted),
                IsActive        = x.IsActive,
                CreatedByUserId = x.CreatedByUserId,
                CreatedAt       = x.CreatedAt,
                UpdatedAt       = x.UpdatedAt,
            })
            .ToListAsync(ct);

        return new PagedList<SpaceResponse>(items, filter.Page, filter.PageSize, total);
    }

    public async Task<Result<SpaceResponse>> GetByIdAsync(long id, long callerUserId, CancellationToken ct)
    {
        var space = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.Id == id && !s.IsDeleted)
            .Select(s => new SpaceResponse
            {
                Id              = s.Id,
                OrganizationId  = s.OrganizationId,
                Name            = s.Name,
                Slug            = s.Slug,
                BoardType       = s.BoardType,
                IssueKeyPrefix  = s.IssueKeyPrefix,
                IssueCounter    = s.IssueCounter,
                IssueCount      = s.Tickets.Count(t => !t.IsDeleted),
                IsActive        = s.IsActive,
                CreatedByUserId = s.CreatedByUserId,
                CreatedAt       = s.CreatedAt,
                UpdatedAt       = s.UpdatedAt,
            })
            .SingleOrDefaultAsync(ct);

        if (space is null) return SpaceErrors.NotFound;
        if (!await IsMemberAsync(space.OrganizationId, callerUserId, ct)) return SpaceErrors.Forbidden;
        return space;
    }

    private async Task<bool> IsMemberAsync(long orgId, long callerUserId, CancellationToken ct)
    {
        var isAdmin = await dbContext.Users.AsNoTracking()
            .AnyAsync(u => u.Id == callerUserId && u.IsSystemAdmin && !u.IsDeleted, ct);
        if (isAdmin) return true;

        return await dbContext.OrgMembers.AsNoTracking()
            .AnyAsync(m => m.OrganizationId == orgId && m.UserId == callerUserId && !m.IsDeleted, ct);
    }

    private async Task<bool> IsAuthorizedAsync(long orgId, long callerUserId, CancellationToken ct)
    {
        var isAdmin = await dbContext.Users.AsNoTracking()
            .AnyAsync(u => u.Id == callerUserId && u.IsSystemAdmin && !u.IsDeleted, ct);
        if (isAdmin) return true;

        return await dbContext.OrgMembers.AsNoTracking()
            .AnyAsync(m => m.OrganizationId == orgId && m.UserId == callerUserId
                        && m.Role == OrgRole.OrgManager && !m.IsDeleted, ct);
    }

    public async Task<Result> AddAsync(long orgId, CreateSpaceRequest request, long callerUserId, CancellationToken ct)
    {
        if (!await IsAuthorizedAsync(orgId, callerUserId, ct)) return SpaceErrors.Forbidden;

        var slugTaken = await dbContext.Spaces
            .AsNoTracking()
            .AnyAsync(s => s.OrganizationId == orgId && s.Slug == request.Slug && !s.IsDeleted, ct);
        if (slugTaken) return SpaceErrors.SlugAlreadyExists;

        var space = new SpaceEntity
        {
            OrganizationId  = orgId,
            Name            = request.Name,
            Slug            = request.Slug,
            BoardType       = request.BoardType,
            IssueKeyPrefix  = request.IssueKeyPrefix.ToUpper(),
            CreatedByUserId = callerUserId,
        };

        dbContext.Spaces.Add(space);

        space.WorkflowStatuses =
        [
            new() { Name = "Backlog",     Color = "#64748b", Position = 0, BaseType = WorkflowStatusBaseType.Backlog },
            new() { Name = "To Do",       Color = "#94a3b8", Position = 1, BaseType = WorkflowStatusBaseType.ToDo },
            new() { Name = "In Progress", Color = "#3b82f6", Position = 2, BaseType = WorkflowStatusBaseType.InProgress },
            new() { Name = "Done",        Color = "#22c55e", Position = 3, BaseType = WorkflowStatusBaseType.Done },
        ];

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UpdateAsync(long id, UpdateSpaceRequest request, long callerUserId, CancellationToken ct)
    {
        var space = await dbContext.Spaces
            .SingleOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        if (space is null) return SpaceErrors.NotFound;

        if (!await IsAuthorizedAsync(space.OrganizationId, callerUserId, ct)) return SpaceErrors.Forbidden;

        space.Name     = request.Name;
        space.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(long id, long callerUserId, CancellationToken ct)
    {
        var space = await dbContext.Spaces
            .SingleOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        if (space is null) return SpaceErrors.NotFound;

        if (!await IsAuthorizedAsync(space.OrganizationId, callerUserId, ct)) return SpaceErrors.Forbidden;

        space.IsDeleted = true;
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }
}
