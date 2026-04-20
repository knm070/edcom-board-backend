using Edcom.TaskManager.Application.Services.Ticket.Contracts;
using Edcom.TaskManager.Domain.Enums;
using Edcom.TaskManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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

    public async Task<Result<List<TicketResponse>>> GetAllBySpaceAsync(long spaceId, long callerUserId, CancellationToken ct)
    {
        if (!await IsMemberBySpaceAsync(spaceId, callerUserId, ct)) return TicketErrors.Forbidden;

        var tickets = await dbContext.Tickets
            .AsNoTracking()
            .Where(t => t.SpaceId == spaceId && !t.IsDeleted)
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
            SprintId        = request.SprintId,
            EpicId          = request.EpicId,
            ReporterId      = callerUserId,
            AssigneeId      = request.AssigneeId,
            DueDate         = request.DueDate,
            EstimationHours = request.EstimationHours,
            BacklogOrder    = maxOrder + 1,
            StoryPoints     = request.StoryPoints,
        };

        // Auto-sync SprintId with status type
        if (request.StatusId.HasValue)
        {
            var statusBaseType = await dbContext.WorkflowStatuses
                .AsNoTracking()
                .Where(s => s.Id == request.StatusId.Value && !s.IsDeleted)
                .Select(s => (WorkflowStatusBaseType?)s.BaseType)
                .SingleOrDefaultAsync(ct);

            if (statusBaseType == WorkflowStatusBaseType.Backlog)
            {
                ticket.SprintId = null;
            }
            else if (!request.SprintId.HasValue)
            {
                ticket.SprintId = await dbContext.Sprints
                    .AsNoTracking()
                    .Where(s => s.SpaceId == spaceId && s.Status == SprintStatus.Active && !s.IsDeleted)
                    .Select(s => (long?)s.Id)
                    .FirstOrDefaultAsync(ct);
            }
        }

        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync(ct);

        if (request.TagIds.Count > 0)
        {
            foreach (var tagId in request.TagIds)
                dbContext.TicketTags.Add(new TicketTagEntity { TicketId = ticket.Id, TagId = tagId });

            await dbContext.SaveChangesAsync(ct);
        }

        return await GetByIdAsync(ticket.Id, callerUserId, ct);
    }

    public async Task<Result> UpdateAsync(long id, UpdateTicketRequest request, long callerUserId, CancellationToken ct)
    {
        var ticket = await dbContext.Tickets
            .Include(t => t.TicketTags)
            .SingleOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);
        if (ticket is null) return TicketErrors.NotFound;
        if (!await IsMemberBySpaceAsync(ticket.SpaceId, callerUserId, ct)) return TicketErrors.Forbidden;

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
                // Moving to backlog status → remove from sprint
                ticket.SprintId = null;
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
}
