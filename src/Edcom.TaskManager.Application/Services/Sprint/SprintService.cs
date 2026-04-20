using Edcom.TaskManager.Application.Services.Sprint.Contracts;
using Edcom.TaskManager.Domain.Enums;
using Edcom.TaskManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SprintEntity = Edcom.TaskManager.Domain.Entities.Sprint;

namespace Edcom.TaskManager.Application.Services.Sprint;

public class SprintService(AppDbContext dbContext) : ISprintService
{
    public async Task<Result<List<SprintResponse>>> GetAllBySpaceAsync(long spaceId, CancellationToken ct)
    {
        var items = await dbContext.Sprints
            .AsNoTracking()
            .Where(s => s.SpaceId == spaceId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SprintResponse
            {
                Id              = s.Id,
                SpaceId         = s.SpaceId,
                Name            = s.Name,
                Goal            = s.Goal,
                StartDate       = s.StartDate,
                EndDate         = s.EndDate,
                Status          = s.Status,
                CreatedByUserId = s.CreatedByUserId,
                CreatedAt       = s.CreatedAt,
                UpdatedAt       = s.UpdatedAt,
            })
            .ToListAsync(ct);

        return items;
    }

    public async Task<Result<SprintResponse>> GetByIdAsync(long id, CancellationToken ct)
    {
        var sprint = await dbContext.Sprints
            .AsNoTracking()
            .Where(s => s.Id == id && !s.IsDeleted)
            .Select(s => new SprintResponse
            {
                Id              = s.Id,
                SpaceId         = s.SpaceId,
                Name            = s.Name,
                Goal            = s.Goal,
                StartDate       = s.StartDate,
                EndDate         = s.EndDate,
                Status          = s.Status,
                CreatedByUserId = s.CreatedByUserId,
                CreatedAt       = s.CreatedAt,
                UpdatedAt       = s.UpdatedAt,
            })
            .SingleOrDefaultAsync(ct);

        return sprint is null ? SprintErrors.NotFound : sprint;
    }

    public async Task<Result> AddAsync(long spaceId, CreateSprintRequest request, long callerUserId, CancellationToken ct)
    {
        var space = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.Id == spaceId && !s.IsDeleted)
            .Select(s => new { s.OrganizationId })
            .SingleOrDefaultAsync(ct);
        if (space is null) return SprintErrors.NotFound;

        var isManager = await dbContext.OrgMembers
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == space.OrganizationId && m.UserId == callerUserId
                        && m.Role == OrgRole.OrgManager && !m.IsDeleted, ct);
        if (!isManager) return SprintErrors.Forbidden;

        dbContext.Sprints.Add(new SprintEntity
        {
            SpaceId         = spaceId,
            Name            = request.Name,
            Goal            = request.Goal,
            StartDate       = request.StartDate,
            EndDate         = request.EndDate,
            CreatedByUserId = callerUserId,
        });

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UpdateAsync(long id, UpdateSprintRequest request, long callerUserId, CancellationToken ct)
    {
        var sprint = await dbContext.Sprints
            .SingleOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        if (sprint is null) return SprintErrors.NotFound;

        var space = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.Id == sprint.SpaceId && !s.IsDeleted)
            .Select(s => new { s.OrganizationId })
            .SingleOrDefaultAsync(ct);

        var isManager = space is not null && await dbContext.OrgMembers
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == space.OrganizationId && m.UserId == callerUserId
                        && m.Role == OrgRole.OrgManager && !m.IsDeleted, ct);
        if (!isManager) return SprintErrors.Forbidden;

        sprint.Name      = request.Name;
        sprint.Goal      = request.Goal;
        sprint.StartDate = request.StartDate;
        sprint.EndDate   = request.EndDate;
        sprint.Status    = request.Status;

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(long id, long callerUserId, CancellationToken ct)
    {
        var sprint = await dbContext.Sprints
            .SingleOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        if (sprint is null) return SprintErrors.NotFound;

        var space = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.Id == sprint.SpaceId && !s.IsDeleted)
            .Select(s => new { s.OrganizationId })
            .SingleOrDefaultAsync(ct);

        var isManager = space is not null && await dbContext.OrgMembers
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == space.OrganizationId && m.UserId == callerUserId
                        && m.Role == OrgRole.OrgManager && !m.IsDeleted, ct);
        if (!isManager) return SprintErrors.Forbidden;

        sprint.IsDeleted = true;
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }
}
