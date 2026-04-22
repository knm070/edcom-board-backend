using Edcom.TaskManager.Application.Services.Sprint.Contracts;
using Edcom.TaskManager.Domain.Enums;
using Edcom.TaskManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SprintEntity = Edcom.TaskManager.Domain.Entities.Sprint;

namespace Edcom.TaskManager.Application.Services.Sprint;

public class SprintService(AppDbContext dbContext) : ISprintService
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

    public async Task<Result<PagedList<SprintResponse>>> GetAllBySpaceAsync(long spaceId, SprintFilterRequest filter, CancellationToken ct)
    {
        var query = dbContext.Sprints
            .AsNoTracking()
            .Where(s => s.SpaceId == spaceId && !s.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = $"%{filter.Search}%";
            query = query.Where(x => EF.Functions.ILike(x.Name, s) || EF.Functions.ILike(x.Goal, s));
        }

        if (filter.Status.HasValue)
            query = query.Where(x => x.Status == filter.Status.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(x => new SprintResponse
            {
                Id              = x.Id,
                SpaceId         = x.SpaceId,
                Name            = x.Name,
                Goal            = x.Goal,
                StartDate       = x.StartDate,
                EndDate         = x.EndDate,
                Status          = x.Status,
                CreatedByUserId = x.CreatedByUserId,
                CreatedAt       = x.CreatedAt,
                UpdatedAt       = x.UpdatedAt,
            })
            .ToListAsync(ct);

        return new PagedList<SprintResponse>(items, filter.Page, filter.PageSize, total);
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

        if (!await IsAuthorizedAsync(space.OrganizationId, callerUserId, ct))
            return SprintErrors.Forbidden;

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

        if (space is null || !await IsAuthorizedAsync(space.OrganizationId, callerUserId, ct))
            return SprintErrors.Forbidden;

        sprint.Name      = request.Name;
        sprint.Goal      = request.Goal;
        sprint.StartDate = request.StartDate;
        sprint.EndDate   = request.EndDate;
        sprint.Status    = request.Status;

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> StartAsync(long sprintId, long callerUserId, CancellationToken ct)
    {
        var sprint = await dbContext.Sprints
            .SingleOrDefaultAsync(s => s.Id == sprintId && !s.IsDeleted, ct);
        if (sprint is null) return SprintErrors.NotFound;

        var space = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.Id == sprint.SpaceId && !s.IsDeleted)
            .Select(s => new { s.OrganizationId })
            .SingleOrDefaultAsync(ct);

        if (space is null || !await IsAuthorizedAsync(space.OrganizationId, callerUserId, ct))
            return SprintErrors.Forbidden;

        sprint.Status    = SprintStatus.Active;
        sprint.StartDate ??= DateTime.UtcNow;

        // Move all backlog-status tickets in this sprint to the initial (To Do) status
        var toDoStatusId = await dbContext.WorkflowStatuses
            .AsNoTracking()
            .Where(w => w.SpaceId == sprint.SpaceId && !w.IsDeleted && w.BaseType == WorkflowStatusBaseType.ToDo)
            .Select(w => (long?)w.Id)
            .FirstOrDefaultAsync(ct);

        if (toDoStatusId.HasValue)
        {
            var backlogTickets = await dbContext.Tickets
                .Include(t => t.Status)
                .Where(t => t.SprintId == sprintId && !t.IsDeleted
                         && t.Status != null && t.Status.BaseType == WorkflowStatusBaseType.Backlog)
                .ToListAsync(ct);

            foreach (var ticket in backlogTickets)
                ticket.StatusId = toDoStatusId.Value;
        }

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> CompleteAsync(long sprintId, CompleteSprintRequest request, long callerUserId, CancellationToken ct)
    {
        var sprint = await dbContext.Sprints
            .SingleOrDefaultAsync(s => s.Id == sprintId && !s.IsDeleted, ct);
        if (sprint is null) return SprintErrors.NotFound;

        var space = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.Id == sprint.SpaceId && !s.IsDeleted)
            .Select(s => new { s.OrganizationId })
            .SingleOrDefaultAsync(ct);

        if (space is null || !await IsAuthorizedAsync(space.OrganizationId, callerUserId, ct))
            return SprintErrors.Forbidden;

        sprint.Status  = SprintStatus.Completed;
        sprint.EndDate ??= DateTime.UtcNow;

        // Find all incomplete tickets (not Done status)
        var incompleteTickets = await dbContext.Tickets
            .Include(t => t.Status)
            .Where(t => t.SprintId == sprintId && !t.IsDeleted
                     && (t.Status == null || t.Status.BaseType != WorkflowStatusBaseType.Done))
            .ToListAsync(ct);

        if (request.Disposition == "next_sprint" && request.TargetSprintId.HasValue)
        {
            foreach (var ticket in incompleteTickets)
                ticket.SprintId = request.TargetSprintId.Value;
        }
        else
        {
            var backlogStatusId = await dbContext.WorkflowStatuses
                .AsNoTracking()
                .Where(w => w.SpaceId == sprint.SpaceId && !w.IsDeleted && w.BaseType == WorkflowStatusBaseType.Backlog)
                .Select(w => (long?)w.Id)
                .FirstOrDefaultAsync(ct);

            foreach (var ticket in incompleteTickets)
            {
                ticket.SprintId = null;
                if (backlogStatusId.HasValue)
                    ticket.StatusId = backlogStatusId.Value;
            }
        }

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

        if (space is null || !await IsAuthorizedAsync(space.OrganizationId, callerUserId, ct))
            return SprintErrors.Forbidden;

        sprint.IsDeleted = true;
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }
}
