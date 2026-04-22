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
            .OrderBy(e => e.RankOrder)
            .ThenByDescending(e => e.CreatedAt)
            .Select(e => new EpicResponse
            {
                Id              = e.Id,
                SpaceId         = e.SpaceId,
                Title           = e.Title,
                Description     = e.Description,
                Color           = e.Color,
                StartDate       = e.StartDate,
                EndDate         = e.EndDate,
                Status          = e.Status,
                OwnerId         = e.OwnerId,
                RankOrder       = e.RankOrder,
                CreatedByUserId = e.CreatedByUserId,
                CreatedAt       = e.CreatedAt,
                UpdatedAt       = e.UpdatedAt,

                TotalTickets = e.Tickets.Count(t => !t.IsDeleted),

                DoneTickets = e.Tickets.Count(t => !t.IsDeleted
                    && t.Status != null
                    && t.Status.BaseType == WorkflowStatusBaseType.Done),

                InProgressTickets = e.Tickets.Count(t => !t.IsDeleted
                    && t.Status != null
                    && (t.Status.BaseType == WorkflowStatusBaseType.InProgress
                     || t.Status.BaseType == WorkflowStatusBaseType.InReview)),

                ToDoTickets = e.Tickets.Count(t => !t.IsDeleted
                    && (t.Status == null
                     || (t.Status.BaseType != WorkflowStatusBaseType.Done
                      && t.Status.BaseType != WorkflowStatusBaseType.InProgress
                      && t.Status.BaseType != WorkflowStatusBaseType.InReview))),

                TotalStoryPoints = e.Tickets
                    .Where(t => !t.IsDeleted)
                    .Sum(t => (int?)t.StoryPoints ?? 0),

                CompletedStoryPoints = e.Tickets
                    .Where(t => !t.IsDeleted
                             && t.Status != null
                             && t.Status.BaseType == WorkflowStatusBaseType.Done)
                    .Sum(t => (int?)t.StoryPoints ?? 0),

                // Computed after fetch:
                PercentComplete          = 0,
                PercentCompleteByPoints  = 0,
            })
            .ToListAsync(ct);

        foreach (var item in items)
        {
            item.PercentComplete = item.TotalTickets > 0
                ? (int)Math.Round((double)item.DoneTickets / item.TotalTickets * 100)
                : 0;

            item.PercentCompleteByPoints = item.TotalStoryPoints > 0
                ? (int)Math.Round((double)item.CompletedStoryPoints / item.TotalStoryPoints * 100)
                : 0;
        }

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
                Status          = e.Status,
                OwnerId         = e.OwnerId,
                RankOrder       = e.RankOrder,
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

        var maxRank = await dbContext.Epics.AsNoTracking()
            .Where(e => e.SpaceId == spaceId && !e.IsDeleted)
            .MaxAsync(e => (long?)e.RankOrder, ct) ?? 0;

        dbContext.Epics.Add(new EpicEntity
        {
            SpaceId         = spaceId,
            Title           = request.Title,
            Description     = request.Description,
            Color           = request.Color,
            StartDate       = request.StartDate,
            EndDate         = request.EndDate,
            Status          = request.Status,
            OwnerId         = request.OwnerId,
            RankOrder       = maxRank + 1,
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
        epic.Status      = request.Status;
        epic.OwnerId     = request.OwnerId;

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

        var hasTickets = await dbContext.Tickets
            .AnyAsync(t => t.EpicId == id && !t.IsDeleted, ct);
        if (hasTickets) return EpicErrors.HasActiveTickets;

        epic.IsDeleted = true;
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<EpicProgressDto>> GetProgressAsync(long epicId, CancellationToken ct)
    {
        var exists = await dbContext.Epics.AsNoTracking()
            .AnyAsync(e => e.Id == epicId && !e.IsDeleted, ct);
        if (!exists) return EpicErrors.NotFound;

        var tickets = await dbContext.Tickets.AsNoTracking()
            .Where(t => t.EpicId == epicId && !t.IsDeleted)
            .Select(t => new
            {
                t.StoryPoints,
                BaseType = t.Status == null ? (WorkflowStatusBaseType?)null : t.Status.BaseType,
            })
            .ToListAsync(ct);

        var total    = tickets.Count;
        var done     = tickets.Count(t => t.BaseType == WorkflowStatusBaseType.Done);
        var inProg   = tickets.Count(t => t.BaseType == WorkflowStatusBaseType.InProgress
                                       || t.BaseType == WorkflowStatusBaseType.InReview);
        var toDo     = total - done - inProg;
        var totalSP  = tickets.Sum(t => t.StoryPoints ?? 0);
        var doneSP   = tickets.Where(t => t.BaseType == WorkflowStatusBaseType.Done)
                              .Sum(t => t.StoryPoints ?? 0);

        return new EpicProgressDto
        {
            EpicId                 = epicId,
            TotalTickets           = total,
            DoneTickets            = done,
            InProgressTickets      = inProg,
            ToDoTickets            = toDo,
            PercentComplete        = total > 0 ? (int)Math.Round((double)done / total * 100) : 0,
            TotalStoryPoints       = totalSP,
            CompletedStoryPoints   = doneSP,
            PercentCompleteByPoints = totalSP > 0 ? (int)Math.Round((double)doneSP / totalSP * 100) : 0,
        };
    }

    public async Task<Result> UpdateStatusAsync(long epicId, UpdateEpicStatusRequest request, long callerUserId, CancellationToken ct)
    {
        var epic = await dbContext.Epics
            .SingleOrDefaultAsync(e => e.Id == epicId && !e.IsDeleted, ct);
        if (epic is null) return EpicErrors.NotFound;

        var space = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.Id == epic.SpaceId && !s.IsDeleted)
            .Select(s => new { s.OrganizationId })
            .SingleOrDefaultAsync(ct);

        if (space is null || !await IsAuthorizedAsync(space.OrganizationId, callerUserId, ct))
            return EpicErrors.Forbidden;

        if (!Enum.IsDefined(typeof(EpicStatus), request.Status))
            return EpicErrors.InvalidStatus;

        epic.Status = request.Status;
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ReorderAsync(long spaceId, ReorderEpicsRequest request, long callerUserId, CancellationToken ct)
    {
        var space = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.Id == spaceId && !s.IsDeleted)
            .Select(s => new { s.OrganizationId })
            .SingleOrDefaultAsync(ct);
        if (space is null) return EpicErrors.NotFound;

        if (!await IsAuthorizedAsync(space.OrganizationId, callerUserId, ct))
            return EpicErrors.Forbidden;

        var epics = await dbContext.Epics
            .Where(e => e.SpaceId == spaceId && !e.IsDeleted && request.EpicIds.Contains(e.Id))
            .ToListAsync(ct);

        for (int i = 0; i < request.EpicIds.Count; i++)
        {
            var epic = epics.SingleOrDefault(e => e.Id == request.EpicIds[i]);
            if (epic is not null) epic.RankOrder = i + 1;
        }

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> AssignTicketAsync(long epicId, long ticketId, long callerUserId, CancellationToken ct)
    {
        var epic = await dbContext.Epics
            .AsNoTracking()
            .Where(e => e.Id == epicId && !e.IsDeleted)
            .Select(e => new { e.SpaceId, OrgId = e.Space.OrganizationId })
            .SingleOrDefaultAsync(ct);
        if (epic is null) return EpicErrors.NotFound;

        if (!await IsAuthorizedAsync(epic.OrgId, callerUserId, ct))
            return EpicErrors.Forbidden;

        var ticket = await dbContext.Tickets
            .SingleOrDefaultAsync(t => t.Id == ticketId && !t.IsDeleted, ct);
        if (ticket is null) return EpicErrors.NotFound;

        if (ticket.SpaceId != epic.SpaceId) return EpicErrors.SpaceMismatch;

        ticket.EpicId = epicId;
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DetachTicketAsync(long epicId, long ticketId, long callerUserId, CancellationToken ct)
    {
        var epic = await dbContext.Epics
            .AsNoTracking()
            .Where(e => e.Id == epicId && !e.IsDeleted)
            .Select(e => new { e.SpaceId, OrgId = e.Space.OrganizationId })
            .SingleOrDefaultAsync(ct);
        if (epic is null) return EpicErrors.NotFound;

        if (!await IsAuthorizedAsync(epic.OrgId, callerUserId, ct))
            return EpicErrors.Forbidden;

        var ticket = await dbContext.Tickets
            .SingleOrDefaultAsync(t => t.Id == ticketId && t.EpicId == epicId && !t.IsDeleted, ct);
        if (ticket is null) return EpicErrors.NotFound;

        ticket.EpicId = null;
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }
}
