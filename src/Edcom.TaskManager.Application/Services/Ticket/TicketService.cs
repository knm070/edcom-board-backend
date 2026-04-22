using Edcom.TaskManager.Application.Services.Ticket.Contracts;
using Edcom.TaskManager.Domain.Enums;
using Edcom.TaskManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ActivityLogEntity = Edcom.TaskManager.Domain.Entities.ActivityLog;
using TicketEntity = Edcom.TaskManager.Domain.Entities.Ticket;
using TicketTagEntity = Edcom.TaskManager.Domain.Entities.TicketTag;

namespace Edcom.TaskManager.Application.Services.Ticket;

public class TicketService(AppDbContext dbContext) : ITicketService
{
    private async Task<bool> IsMemberBySpaceAsync(long spaceId, long callerUserId, CancellationToken ct)
    {
        var isAdmin = await dbContext.Users.AsNoTracking()
            .AnyAsync(u => u.Id == callerUserId && u.IsSystemAdmin && !u.IsDeleted, ct);
        if (isAdmin) return true;

        var orgId = await dbContext.Spaces.AsNoTracking()
            .Where(s => s.Id == spaceId && !s.IsDeleted)
            .Select(s => (long?)s.OrganizationId)
            .FirstOrDefaultAsync(ct);
        if (orgId is null) return false;

        return await dbContext.OrgMembers.AsNoTracking()
            .AnyAsync(m => m.OrganizationId == orgId && m.UserId == callerUserId && !m.IsDeleted, ct);
    }

    private static TicketResponse MapTicket(Domain.Entities.Ticket t) => new()
    {
        Id              = t.Id,
        SpaceId         = t.SpaceId,
        OrganizationId  = t.OrganizationId,
        KeyNumber       = t.KeyNumber,
        Type            = t.Type,
        Priority        = t.Priority,
        Title           = t.Title,
        Description     = t.Description,
        StatusId        = t.StatusId,
        StatusName      = t.Status?.Name,
        StatusColor     = t.Status?.Color,
        StatusBaseType  = (int?)t.Status?.BaseType,
        SprintId        = t.SprintId,
        SprintName      = t.Sprint?.Name,
        EpicId          = t.EpicId,
        EpicTitle       = t.Epic?.Title,
        ReporterId      = t.ReporterId,
        ReporterName    = t.Reporter.FullName,
        AssigneeId      = t.AssigneeId,
        AssigneeName    = t.Assignee?.FullName,
        DueDate         = t.DueDate,
        EstimationHours = t.EstimationHours,
        BacklogOrder    = t.BacklogOrder,
        StoryPoints     = t.StoryPoints,
        Tags            = t.TicketTags.Select(tt => new TagSummary { Id = tt.TagId, Name = tt.Tag.Name, Color = tt.Tag.Color }).ToList(),
        CreatedAt       = t.CreatedAt,
        UpdatedAt       = t.UpdatedAt,
    };

    public async Task<Result<List<TicketResponse>>> GetAllBySpaceAsync(long spaceId, GetTicketsFilterRequest filter, long callerUserId, CancellationToken ct)
    {
        if (!await IsMemberBySpaceAsync(spaceId, callerUserId, ct)) return TicketErrors.Forbidden;

        var query = dbContext.Tickets
            .AsNoTracking()
            .Where(t => t.SpaceId == spaceId && !t.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(t => t.KeyNumber.ToLower().Contains(s) || t.Title.ToLower().Contains(s));
        }

        if (filter.AssigneeIds is { Count: > 0 })
            query = query.Where(t => t.AssigneeId.HasValue && filter.AssigneeIds.Contains(t.AssigneeId.Value));

        if (filter.Priorities is { Count: > 0 })
            query = query.Where(t => filter.Priorities.Contains((int)t.Priority));

        if (filter.Types is { Count: > 0 })
            query = query.Where(t => filter.Types.Contains((int)t.Type));

        if (filter.EpicIds is { Count: > 0 })
            query = query.Where(t => t.EpicId.HasValue && filter.EpicIds.Contains(t.EpicId.Value));

        if (filter.SprintId.HasValue)
            query = query.Where(t => t.SprintId == filter.SprintId);

        if (filter.Backlog == true)
            query = query.Where(t => t.Status != null && (int)t.Status.BaseType == 5);

        var tickets = await query
            .Include(t => t.Status)
            .Include(t => t.Sprint)
            .Include(t => t.Epic)
            .Include(t => t.Reporter)
            .Include(t => t.Assignee)
            .Include(t => t.TicketTags).ThenInclude(tt => tt.Tag)
            .OrderBy(t => t.BacklogOrder)
            .ToListAsync(ct);

        return tickets.Select(MapTicket).ToList();
    }

    public async Task<Result<TicketResponse>> GetByIdAsync(long id, long callerUserId, CancellationToken ct)
    {
        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .Where(t => t.Id == id && !t.IsDeleted)
            .Include(t => t.Status)
            .Include(t => t.Sprint)
            .Include(t => t.Epic)
            .Include(t => t.Reporter)
            .Include(t => t.Assignee)
            .Include(t => t.TicketTags).ThenInclude(tt => tt.Tag)
            .SingleOrDefaultAsync(ct);

        if (ticket is null) return TicketErrors.NotFound;
        if (!await IsMemberBySpaceAsync(ticket.SpaceId, callerUserId, ct)) return TicketErrors.Forbidden;
        return MapTicket(ticket);
    }

    public async Task<Result<TicketResponse>> AddAsync(long spaceId, CreateTicketRequest request, long callerUserId, CancellationToken ct)
    {
        var space = await dbContext.Spaces
            .SingleOrDefaultAsync(s => s.Id == spaceId && !s.IsDeleted, ct);
        if (space is null) return TicketErrors.SpaceNotFound;

        if (!await IsMemberBySpaceAsync(spaceId, callerUserId, ct)) return TicketErrors.Forbidden;

        space.IssueCounter++;
        var keyNumber = $"{space.IssueKeyPrefix}-{space.IssueCounter}";

        var maxOrder = await dbContext.Tickets
            .AsNoTracking()
            .Where(t => t.SpaceId == spaceId && !t.IsDeleted)
            .MaxAsync(t => (int?)t.BacklogOrder, ct) ?? 0;

        // Resolve sprint: auto-assign to active sprint unless status is Backlog
        long? resolvedSprintId = request.SprintId;
        if (request.StatusId.HasValue)
        {
            var statusBaseType = await dbContext.WorkflowStatuses
                .AsNoTracking()
                .Where(s => s.Id == request.StatusId.Value && !s.IsDeleted)
                .Select(s => (WorkflowStatusBaseType?)s.BaseType)
                .SingleOrDefaultAsync(ct);

            if (statusBaseType == WorkflowStatusBaseType.Backlog)
            {
                // Backlog status + explicit sprintId = pre-assigned to planned sprint (stays in backlog)
                // Backlog status + no sprintId = standalone backlog ticket
                resolvedSprintId = request.SprintId;
            }
            else if (!request.SprintId.HasValue)
            {
                resolvedSprintId = await dbContext.Sprints
                    .AsNoTracking()
                    .Where(s => s.SpaceId == spaceId && s.Status == SprintStatus.Active && !s.IsDeleted)
                    .Select(s => (long?)s.Id)
                    .FirstOrDefaultAsync(ct);
            }
        }

        var ticket = new TicketEntity
        {
            SpaceId         = spaceId,
            OrganizationId  = space.OrganizationId,
            KeyNumber       = keyNumber,
            Type            = request.Type,
            Priority        = request.Priority,
            Title           = request.Title,
            Description     = request.Description,
            StatusId        = request.StatusId,
            SprintId        = resolvedSprintId,
            EpicId          = request.EpicId,
            ReporterId      = callerUserId,
            AssigneeId      = request.AssigneeId,
            DueDate         = request.DueDate,
            EstimationHours = request.EstimationHours,
            BacklogOrder    = maxOrder + 1,
            StoryPoints     = request.StoryPoints,
            TicketTags      = request.TagIds.Select(id => new TicketTagEntity { TagId = id }).ToList(),
            ActivityLogs    = [new ActivityLogEntity { ActorId = callerUserId, Action = ActivityAction.Created }],
        };

        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(ticket.Id, callerUserId, ct);
    }

    public async Task<Result> UpdateAsync(long id, UpdateTicketRequest request, long callerUserId, CancellationToken ct)
    {
        var ticket = await dbContext.Tickets
            .Include(t => t.TicketTags)
            .SingleOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);
        if (ticket is null) return TicketErrors.NotFound;
        if (!await IsMemberBySpaceAsync(ticket.SpaceId, callerUserId, ct)) return TicketErrors.Forbidden;

        // Capture old values before any assignments
        var oldPriority    = ticket.Priority;
        var oldTitle       = ticket.Title;
        var oldDescription = ticket.Description;
        var oldStatusId    = ticket.StatusId;
        var oldSprintId    = ticket.SprintId;
        var oldEpicId      = ticket.EpicId;
        var oldAssigneeId  = ticket.AssigneeId;
        var oldDueDate     = ticket.DueDate;

        ticket.Type            = request.Type;
        ticket.Priority        = request.Priority;
        ticket.Title           = request.Title;
        ticket.Description     = request.Description;
        ticket.StatusId        = request.StatusId;

        // Auto-sync SprintId based on the new status type
        if (request.StatusId.HasValue)
        {
            var statusBaseType = await dbContext.WorkflowStatuses
                .AsNoTracking()
                .Where(s => s.Id == request.StatusId.Value && !s.IsDeleted)
                .Select(s => (WorkflowStatusBaseType?)s.BaseType)
                .SingleOrDefaultAsync(ct);

            if (statusBaseType == WorkflowStatusBaseType.Backlog)
            {
                // Backlog status + explicit sprintId = pre-assigned to a planned sprint (still in backlog)
                // Backlog status + no sprintId = standalone backlog (remove from sprint)
                ticket.SprintId = request.SprintId;
            }
            else if (ticket.SprintId == null)
            {
                // Moving from backlog to workflow → auto-assign to active sprint if available
                ticket.SprintId = await dbContext.Sprints
                    .AsNoTracking()
                    .Where(s => s.SpaceId == ticket.SpaceId && s.Status == SprintStatus.Active && !s.IsDeleted)
                    .Select(s => (long?)s.Id)
                    .FirstOrDefaultAsync(ct);
            }
            // else: ticket already has a sprint — keep it
        }
        else
        {
            ticket.SprintId = request.SprintId;
        }

        ticket.EpicId          = request.EpicId;
        ticket.AssigneeId      = request.AssigneeId;
        ticket.DueDate         = request.DueDate;
        ticket.EstimationHours = request.EstimationHours;
        ticket.BacklogOrder    = request.BacklogOrder;
        ticket.StoryPoints     = request.StoryPoints;

        // Build activity logs for changed fields
        var activityLogs = new List<ActivityLogEntity>();

        if (oldStatusId != ticket.StatusId)
        {
            var statusIds = new[] { oldStatusId, ticket.StatusId }
                .Where(s => s.HasValue).Select(s => s!.Value).Distinct().ToList();
            var statusNames = await dbContext.WorkflowStatuses.AsNoTracking()
                .Where(s => statusIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
            activityLogs.Add(new ActivityLogEntity
            {
                TicketId  = id,
                ActorId   = callerUserId,
                Action    = ActivityAction.StatusChanged,
                OldValue  = oldStatusId.HasValue && statusNames.TryGetValue(oldStatusId.Value, out var osn) ? osn : null,
                NewValue  = ticket.StatusId.HasValue && statusNames.TryGetValue(ticket.StatusId.Value, out var nsn) ? nsn : null,
                CreatedAt = DateTime.UtcNow,
            });
        }

        if (oldPriority != ticket.Priority)
            activityLogs.Add(new ActivityLogEntity
            {
                TicketId  = id,
                ActorId   = callerUserId,
                Action    = ActivityAction.PriorityChanged,
                OldValue  = oldPriority.ToString(),
                NewValue  = ticket.Priority.ToString(),
                CreatedAt = DateTime.UtcNow,
            });

        if (oldTitle != ticket.Title)
            activityLogs.Add(new ActivityLogEntity
            {
                TicketId  = id,
                ActorId   = callerUserId,
                Action    = ActivityAction.TitleChanged,
                OldValue  = oldTitle,
                NewValue  = ticket.Title,
                CreatedAt = DateTime.UtcNow,
            });

        if (oldDescription != ticket.Description)
            activityLogs.Add(new ActivityLogEntity
            {
                TicketId  = id,
                ActorId   = callerUserId,
                Action    = ActivityAction.DescriptionChanged,
                CreatedAt = DateTime.UtcNow,
            });

        if (oldAssigneeId != ticket.AssigneeId)
        {
            var assigneeIds = new[] { oldAssigneeId, ticket.AssigneeId }
                .Where(a => a.HasValue).Select(a => a!.Value).Distinct().ToList();
            var assigneeNames = await dbContext.Users.AsNoTracking()
                .Where(u => assigneeIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
            activityLogs.Add(new ActivityLogEntity
            {
                TicketId  = id,
                ActorId   = callerUserId,
                Action    = ActivityAction.AssigneeChanged,
                OldValue  = oldAssigneeId.HasValue && assigneeNames.TryGetValue(oldAssigneeId.Value, out var oan) ? oan : null,
                NewValue  = ticket.AssigneeId.HasValue && assigneeNames.TryGetValue(ticket.AssigneeId.Value, out var nan) ? nan : null,
                CreatedAt = DateTime.UtcNow,
            });
        }

        if (oldDueDate != ticket.DueDate)
            activityLogs.Add(new ActivityLogEntity
            {
                TicketId  = id,
                ActorId   = callerUserId,
                Action    = ActivityAction.DueDateChanged,
                OldValue  = oldDueDate?.ToString("yyyy-MM-dd"),
                NewValue  = ticket.DueDate?.ToString("yyyy-MM-dd"),
                CreatedAt = DateTime.UtcNow,
            });

        if (oldSprintId != ticket.SprintId)
        {
            var sprintIds = new[] { oldSprintId, ticket.SprintId }
                .Where(s => s.HasValue).Select(s => s!.Value).Distinct().ToList();
            var sprintNames = await dbContext.Sprints.AsNoTracking()
                .Where(s => sprintIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
            activityLogs.Add(new ActivityLogEntity
            {
                TicketId  = id,
                ActorId   = callerUserId,
                Action    = ActivityAction.SprintChanged,
                OldValue  = oldSprintId.HasValue && sprintNames.TryGetValue(oldSprintId.Value, out var osp) ? osp : null,
                NewValue  = ticket.SprintId.HasValue && sprintNames.TryGetValue(ticket.SprintId.Value, out var nsp) ? nsp : null,
                CreatedAt = DateTime.UtcNow,
            });
        }

        if (oldEpicId != ticket.EpicId)
        {
            var epicIds = new[] { oldEpicId, ticket.EpicId }
                .Where(e => e.HasValue).Select(e => e!.Value).Distinct().ToList();
            var epicTitles = await dbContext.Epics.AsNoTracking()
                .Where(e => epicIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.Title, ct);
            activityLogs.Add(new ActivityLogEntity
            {
                TicketId  = id,
                ActorId   = callerUserId,
                Action    = ActivityAction.EpicChanged,
                OldValue  = oldEpicId.HasValue && epicTitles.TryGetValue(oldEpicId.Value, out var oet) ? oet : null,
                NewValue  = ticket.EpicId.HasValue && epicTitles.TryGetValue(ticket.EpicId.Value, out var net) ? net : null,
                CreatedAt = DateTime.UtcNow,
            });
        }

        if (activityLogs.Count > 0)
            dbContext.ActivityLogs.AddRange(activityLogs);

        var existingTagIds = ticket.TicketTags.Select(tt => tt.TagId).ToHashSet();
        var requestedTagIds = request.TagIds.ToHashSet();

        foreach (var tt in ticket.TicketTags.Where(tt => !requestedTagIds.Contains(tt.TagId)).ToList())
            dbContext.TicketTags.Remove(tt);

        foreach (var tagId in requestedTagIds.Except(existingTagIds))
            dbContext.TicketTags.Add(new TicketTagEntity { TicketId = id, TagId = tagId });

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(long id, long callerUserId, CancellationToken ct)
    {
        var ticket = await dbContext.Tickets
            .SingleOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);
        if (ticket is null) return TicketErrors.NotFound;
        if (!await IsMemberBySpaceAsync(ticket.SpaceId, callerUserId, ct)) return TicketErrors.Forbidden;

        ticket.IsDeleted = true;
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<List<ActivityLogResponse>>> GetActivityAsync(long ticketId, long callerUserId, CancellationToken ct)
    {
        var spaceId = await dbContext.Tickets.AsNoTracking()
            .Where(t => t.Id == ticketId && !t.IsDeleted)
            .Select(t => (long?)t.SpaceId)
            .SingleOrDefaultAsync(ct);
        if (spaceId is null) return TicketErrors.NotFound;
        if (!await IsMemberBySpaceAsync(spaceId.Value, callerUserId, ct)) return TicketErrors.Forbidden;

        var logs = await dbContext.ActivityLogs
            .AsNoTracking()
            .Where(a => a.TicketId == ticketId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new ActivityLogResponse
            {
                Id             = a.Id,
                ActorId        = a.ActorId,
                ActorName      = a.Actor.FullName,
                ActorAvatarUrl = a.Actor.AvatarUrl,
                Action         = a.Action.ToString(),
                FieldName      = a.FieldName,
                OldValue       = a.OldValue,
                NewValue       = a.NewValue,
                CreatedAt      = a.CreatedAt,
            })
            .ToListAsync(ct);

        return logs;
    }
}
