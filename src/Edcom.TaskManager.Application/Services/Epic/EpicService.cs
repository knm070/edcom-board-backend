using Edcom.TaskManager.Application.Services.Epic.Contracts;
using Edcom.TaskManager.Domain.Enums;
using Edcom.TaskManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using EpicEntity = Edcom.TaskManager.Domain.Entities.Epic;

namespace Edcom.TaskManager.Application.Services.Epic;

public class EpicService(AppDbContext dbContext) : IEpicService
{
    private async Task<bool> IsAuthorizedAsync(long orgId, long callerUserId, CancellationToken ct)
    {
        var isAdmin = await dbContext.Users.AsNoTracking()
            .AnyAsync(u => u.Id == callerUserId && u.IsSystemAdmin && !u.IsDeleted, ct);
        if (isAdmin) return true;

        return await dbContext.OrgMembers.AsNoTracking()
            .AnyAsync(m => m.OrganizationId == orgId && m.UserId == callerUserId
                        && m.Role == OrgRole.OrgManager && !m.IsDeleted, ct);
    }

    public async Task<Result<List<EpicResponse>>> GetAllBySpaceAsync(long spaceId, CancellationToken ct)
    {
        var items = await dbContext.Epics
            .AsNoTracking()
            .Where(e => e.SpaceId == spaceId && !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new EpicResponse
            {
                Id              = e.Id,
                SpaceId         = e.SpaceId,
                Title           = e.Title,
                Description     = e.Description,
                Color           = e.Color,
                StartDate       = e.StartDate,
                EndDate         = e.EndDate,
                CreatedByUserId = e.CreatedByUserId,
                CreatedAt       = e.CreatedAt,
                UpdatedAt       = e.UpdatedAt,
            })
            .ToListAsync(ct);

        return items;
    }

    public async Task<Result<EpicResponse>> GetByIdAsync(long id, CancellationToken ct)
    {
        var epic = await dbContext.Epics
            .AsNoTracking()
            .Where(e => e.Id == id && !e.IsDeleted)
            .Select(e => new EpicResponse
            {
                Id              = e.Id,
                SpaceId         = e.SpaceId,
                Title           = e.Title,
                Description     = e.Description,
                Color           = e.Color,
                StartDate       = e.StartDate,
                EndDate         = e.EndDate,
                CreatedByUserId = e.CreatedByUserId,
                CreatedAt       = e.CreatedAt,
                UpdatedAt       = e.UpdatedAt,
            })
            .SingleOrDefaultAsync(ct);

        return epic is null ? EpicErrors.NotFound : epic;
    }

    public async Task<Result> AddAsync(long spaceId, CreateEpicRequest request, long callerUserId, CancellationToken ct)
    {
        var space = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.Id == spaceId && !s.IsDeleted)
            .Select(s => new { s.OrganizationId })
            .SingleOrDefaultAsync(ct);
        if (space is null) return EpicErrors.NotFound;

        if (!await IsAuthorizedAsync(space.OrganizationId, callerUserId, ct))
            return EpicErrors.Forbidden;

        dbContext.Epics.Add(new EpicEntity
        {
            SpaceId         = spaceId,
            Title           = request.Title,
            Description     = request.Description,
            Color           = request.Color,
            StartDate       = request.StartDate,
            EndDate         = request.EndDate,
            CreatedByUserId = callerUserId,
        });

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UpdateAsync(long id, UpdateEpicRequest request, long callerUserId, CancellationToken ct)
    {
        var epic = await dbContext.Epics
            .SingleOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);
        if (epic is null) return EpicErrors.NotFound;

        var space = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.Id == epic.SpaceId && !s.IsDeleted)
            .Select(s => new { s.OrganizationId })
            .SingleOrDefaultAsync(ct);

        if (space is null || !await IsAuthorizedAsync(space.OrganizationId, callerUserId, ct))
            return EpicErrors.Forbidden;

        epic.Title       = request.Title;
        epic.Description = request.Description;
        epic.Color       = request.Color;
        epic.StartDate   = request.StartDate;
        epic.EndDate     = request.EndDate;

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(long id, long callerUserId, CancellationToken ct)
    {
        var epic = await dbContext.Epics
            .SingleOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);
        if (epic is null) return EpicErrors.NotFound;

        var space = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.Id == epic.SpaceId && !s.IsDeleted)
            .Select(s => new { s.OrganizationId })
            .SingleOrDefaultAsync(ct);

        if (space is null || !await IsAuthorizedAsync(space.OrganizationId, callerUserId, ct))
            return EpicErrors.Forbidden;

        epic.IsDeleted = true;
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }
}
