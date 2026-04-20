using Edcom.TaskManager.Application.Services.Space.Contracts;
using Edcom.TaskManager.Domain.Enums;
using Edcom.TaskManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SpaceEntity = Edcom.TaskManager.Domain.Entities.Space;

namespace Edcom.TaskManager.Application.Services.Space;

public class SpaceService(AppDbContext dbContext) : ISpaceService
{
    public async Task<Result<List<SpaceResponse>>> GetAllByOrgAsync(long orgId, CancellationToken ct)
    {
        var items = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.OrganizationId == orgId && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .Select(s => new SpaceResponse
            {
                Id              = s.Id,
                OrganizationId  = s.OrganizationId,
                Name            = s.Name,
                Slug            = s.Slug,
                BoardType       = s.BoardType,
                IssueKeyPrefix  = s.IssueKeyPrefix,
                IssueCounter    = s.IssueCounter,
                IsActive        = s.IsActive,
                CreatedByUserId = s.CreatedByUserId,
                CreatedAt       = s.CreatedAt,
                UpdatedAt       = s.UpdatedAt,
            })
            .ToListAsync(ct);

        return items;
    }

    public async Task<Result<SpaceResponse>> GetByIdAsync(long id, CancellationToken ct)
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
                IsActive        = s.IsActive,
                CreatedByUserId = s.CreatedByUserId,
                CreatedAt       = s.CreatedAt,
                UpdatedAt       = s.UpdatedAt,
            })
            .SingleOrDefaultAsync(ct);

        return space is null ? SpaceErrors.NotFound : space;
    }

    public async Task<Result> AddAsync(long orgId, CreateSpaceRequest request, long callerUserId, CancellationToken ct)
    {
        var isManager = await dbContext.OrgMembers
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == orgId && m.UserId == callerUserId
                        && m.Role == OrgRole.OrgManager && !m.IsDeleted, ct);
        if (!isManager) return SpaceErrors.Forbidden;

        var slugTaken = await dbContext.Spaces
            .AsNoTracking()
            .AnyAsync(s => s.OrganizationId == orgId && s.Slug == request.Slug && !s.IsDeleted, ct);
        if (slugTaken) return SpaceErrors.SlugAlreadyExists;

        dbContext.Spaces.Add(new SpaceEntity
        {
            OrganizationId  = orgId,
            Name            = request.Name,
            Slug            = request.Slug,
            BoardType       = request.BoardType,
            IssueKeyPrefix  = request.IssueKeyPrefix.ToUpper(),
            CreatedByUserId = callerUserId,
        });

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UpdateAsync(long id, UpdateSpaceRequest request, long callerUserId, CancellationToken ct)
    {
        var space = await dbContext.Spaces
            .SingleOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        if (space is null) return SpaceErrors.NotFound;

        var isManager = await dbContext.OrgMembers
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == space.OrganizationId && m.UserId == callerUserId
                        && m.Role == OrgRole.OrgManager && !m.IsDeleted, ct);
        if (!isManager) return SpaceErrors.Forbidden;

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

        var isManager = await dbContext.OrgMembers
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == space.OrganizationId && m.UserId == callerUserId
                        && m.Role == OrgRole.OrgManager && !m.IsDeleted, ct);
        if (!isManager) return SpaceErrors.Forbidden;

        space.IsDeleted = true;
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }
}
