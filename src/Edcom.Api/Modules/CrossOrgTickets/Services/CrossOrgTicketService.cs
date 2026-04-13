using System.Security.Claims;
using System.Text.Json;
using Edcom.Api.Infrastructure.Data;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Infrastructure.Hubs;
using Edcom.Api.Modules.Authorization.Extensions;
using Edcom.Api.Modules.Authorization.Services;
using Edcom.Api.Modules.CrossOrgTickets.Dto;
using Edcom.Api.Modules.Spaces.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Edcom.Api.Modules.CrossOrgTickets.Services;

public interface ICrossOrgTicketService
{
    // Step 1 — creator org sends a ticket to receiver org
    Task<CrossOrgTicketDto> SendAsync(Guid creatorOrgId, SendCrossOrgTicketRequest req, ClaimsPrincipal user, CancellationToken ct = default);

    // Step 2 — receiver OrgTaskManager assigns + estimates
    Task<CrossOrgTicketDto> AssignAsync(Guid ticketId, Guid receiverOrgId, AssignTicketRequest req, ClaimsPrincipal user, CancellationToken ct = default);

    // Step 3 — receiver OrgTaskManager activates (Backlog → To Do)
    Task<CrossOrgTicketDto> ActivateAsync(Guid ticketId, Guid receiverOrgId, ClaimsPrincipal user, CancellationToken ct = default);

    // Step 4 — receiver progresses ticket (To Do → In Progress → In Review → Done)
    // Step 5 auto-triggers on Done
    Task<CrossOrgTicketDto> ProgressAsync(Guid ticketId, Guid receiverOrgId, ProgressTicketRequest req, ClaimsPrincipal user, CancellationToken ct = default);

    // Step 6a — creator OrgTaskManager approves: mirror → Done, external closed
    Task<CrossOrgTicketDto> ApproveAsync(Guid ticketId, Guid creatorOrgId, ClaimsPrincipal user, CancellationToken ct = default);

    // Step 6b — creator OrgTaskManager rejects: posts comment, mirror stays InReview
    Task<CrossOrgTicketDto> RejectAsync(Guid ticketId, Guid creatorOrgId, RejectTicketRequest req, ClaimsPrincipal user, CancellationToken ct = default);

    Task<List<CrossOrgTicketDto>> GetSentAsync(Guid creatorOrgId, CancellationToken ct = default);
    Task<List<CrossOrgTicketDto>> GetReceivedAsync(Guid receiverOrgId, CancellationToken ct = default);
}

public class CrossOrgTicketService(
    AppDbContext db,
    IPermissionService perms,
    IWorkflowTransitionService transitions,
    IHubContext<EdcomHub> hub,
    ILogger<CrossOrgTicketService> logger) : ICrossOrgTicketService
{
    private static readonly JsonSerializerOptions _json =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // ── Step 1 — Send ─────────────────────────────────────────────────────────

    public async Task<CrossOrgTicketDto> SendAsync(
        Guid creatorOrgId, SendCrossOrgTicketRequest req,
        ClaimsPrincipal user, CancellationToken ct = default)
    {
        RequirePermission(perms.CanSendCrossOrgTicket(user, creatorOrgId));

        if (!Enum.TryParse<IssuePriority>(req.Priority, out var priority))
            throw new InvalidOperationException($"Invalid priority '{req.Priority}'. Use High or Low.");

        // Resolve receiver's external space
        var receiverExternalSpace = await db.Spaces
            .FirstOrDefaultAsync(s => s.OrgId == req.TargetOrgId && s.Type == SpaceType.External, ct)
            ?? throw new KeyNotFoundException("Target organization has no external space.");

        // Get the fixed "Backlog" system status
        var backlogStatus = await db.WorkflowStatuses
            .FirstOrDefaultAsync(w => w.SpaceId == null && w.Name == "Backlog", ct)
            ?? throw new InvalidOperationException("System external workflow not seeded. Create an organization first.");

        // Increment key counter
        receiverExternalSpace.IssueCounter++;
        receiverExternalSpace.UpdatedAt = DateTime.UtcNow;

        var currentUserId = user.GetUserId();

        var issue = new Issue
        {
            SpaceId             = receiverExternalSpace.Id,
            OrgId               = req.TargetOrgId,
            KeyNumber           = receiverExternalSpace.IssueCounter,
            Title               = req.Title,
            Description         = req.Description,
            Type                = IssueType.Task,
            Priority            = priority,
            StatusId            = backlogStatus.Id,
            ReporterId          = currentUserId,
            FileAttachmentsJson = SerializeAttachments(req.Attachments)
        };
        db.Issues.Add(issue);

        var crossOrgTicket = new CrossOrgTicket
        {
            CreatorOrgId   = creatorOrgId,
            ReceiverOrgId  = req.TargetOrgId,
            ExternalIssueId = issue.Id,
            CreatedById    = currentUserId
        };
        db.CrossOrgTickets.Add(crossOrgTicket);

        await db.SaveChangesAsync(ct);

        // Notify receiver org's OrgTaskManagers
        await hub.Clients.Group($"org:{req.TargetOrgId}")
            .SendAsync("new_external_ticket_received", new
            {
                ticketId     = crossOrgTicket.Id,
                issueId      = issue.Id,
                title        = issue.Title,
                creatorOrgId = creatorOrgId
            }, ct);

        return await LoadDtoAsync(crossOrgTicket.Id, ct);
    }

    // ── Step 2 — Assign ───────────────────────────────────────────────────────

    public async Task<CrossOrgTicketDto> AssignAsync(
        Guid ticketId, Guid receiverOrgId, AssignTicketRequest req,
        ClaimsPrincipal user, CancellationToken ct = default)
    {
        RequirePermission(perms.CanAssignAndEstimate(user, receiverOrgId));

        var (ticket, externalIssue) = await LoadReceiverTicketAsync(ticketId, receiverOrgId, ct);

        // Replace assignee
        var existing = await db.IssueAssignees
            .Where(a => a.IssueId == externalIssue.Id).ToListAsync(ct);
        db.IssueAssignees.RemoveRange(existing);

        if (req.AssigneeId.HasValue)
        {
            // Validate assignee belongs to receiver org
            if (!await db.OrgMembers.AnyAsync(
                m => m.OrgId == receiverOrgId && m.UserId == req.AssigneeId.Value, ct))
                throw new InvalidOperationException("Assignee must be a member of the receiver organization.");

            db.IssueAssignees.Add(new IssueAssignee
            {
                IssueId      = externalIssue.Id,
                UserId       = req.AssigneeId.Value,
                AssignedById = user.GetUserId()
            });
        }

        if (req.EstimationHours.HasValue) externalIssue.StoryPoints = req.EstimationHours;
        if (req.DueDate.HasValue)         externalIssue.DueDate     = req.DueDate;
        externalIssue.UpdatedAt = DateTime.UtcNow;
        ticket.UpdatedAt        = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        // Inform creator org of the update
        await hub.Clients.Group($"org:{ticket.CreatorOrgId}")
            .SendAsync("external_ticket_updated", new { ticketId, stage = "assigned" }, ct);

        return await LoadDtoAsync(ticketId, ct);
    }

    // ── Step 3 — Activate (Backlog → To Do) ──────────────────────────────────

    public async Task<CrossOrgTicketDto> ActivateAsync(
        Guid ticketId, Guid receiverOrgId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        RequirePermission(perms.CanMoveToDo(user, receiverOrgId));

        var (ticket, externalIssue) = await LoadReceiverTicketAsync(ticketId, receiverOrgId, ct);
        await db.Entry(externalIssue).Reference(i => i.Status).LoadAsync(ct);

        if (externalIssue.Status.Name != "Backlog")
            throw new InvalidOperationException("Ticket can only be activated from Backlog status.");

        var toDoStatus = await db.WorkflowStatuses
            .FirstOrDefaultAsync(w => w.SpaceId == null && w.Name == "To Do", ct)
            ?? throw new InvalidOperationException("System 'To Do' status not found.");

        externalIssue.StatusId  = toDoStatus.Id;
        externalIssue.UpdatedAt = DateTime.UtcNow;
        ticket.UpdatedAt        = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        await hub.Clients.Group($"org:{ticket.CreatorOrgId}")
            .SendAsync("external_ticket_activated", new
            {
                ticketId,
                issueId = externalIssue.Id,
                newStatus = "To Do"
            }, ct);

        return await LoadDtoAsync(ticketId, ct);
    }

    // ── Step 4 — Progress + Step 5 auto-trigger on Done ──────────────────────

    public async Task<CrossOrgTicketDto> ProgressAsync(
        Guid ticketId, Guid receiverOrgId, ProgressTicketRequest req,
        ClaimsPrincipal user, CancellationToken ct = default)
    {
        RequirePermission(perms.CanProgressExternalTicket(user, receiverOrgId));

        var (ticket, externalIssue) = await LoadReceiverTicketAsync(ticketId, receiverOrgId, ct);
        await db.Entry(externalIssue).Reference(i => i.Status).LoadAsync(ct);

        var targetStatus = await db.WorkflowStatuses
            .FirstOrDefaultAsync(w => w.SpaceId == null && w.Name == req.TargetStatusName, ct)
            ?? throw new KeyNotFoundException($"External status '{req.TargetStatusName}' not found.");

        // "Done" requires OrgTaskManager
        if (req.TargetStatusName == "Done")
            RequirePermission(perms.CanMoveToDo(user, receiverOrgId),
                "Only OrgTaskManagers can mark a ticket Done.");

        var externalSpace = await db.Spaces.FindAsync([externalIssue.SpaceId], ct)!;
        var callerRole    = user.GetOrgRole(receiverOrgId)
            ?? throw new UnauthorizedAccessException("You are not a member of the receiver organization.");

        await transitions.ValidateAsync(externalSpace!, externalIssue.StatusId, targetStatus.Id, callerRole, ct);

        externalIssue.StatusId  = targetStatus.Id;
        externalIssue.UpdatedAt = DateTime.UtcNow;
        ticket.UpdatedAt        = DateTime.UtcNow;

        // ── Step 5: Auto-trigger on Done ──────────────────────────────────────
        if (req.TargetStatusName == "Done")
        {
            ticket.CompletionNotes = req.CompletionNotes;
            await CreateMirrorIssueAsync(ticket, externalIssue, ct);
        }

        await db.SaveChangesAsync(ct);

        await hub.Clients.Group($"org:{ticket.CreatorOrgId}")
            .SendAsync("external_ticket_status_changed", new
            {
                ticketId,
                issueId   = externalIssue.Id,
                newStatus = req.TargetStatusName
            }, ct);

        if (req.TargetStatusName == "Done")
        {
            await hub.Clients.Group($"org:{ticket.CreatorOrgId}")
                .SendAsync("external_ticket_completed", new { ticketId }, ct);
            await hub.Clients.Group($"org:{ticket.CreatorOrgId}")
                .SendAsync("ticket_returned_for_review", new
                {
                    ticketId,
                    mirrorIssueId = ticket.InternalMirrorIssueId
                }, ct);
        }

        return await LoadDtoAsync(ticketId, ct);
    }

    // ── Step 6a — Approve ─────────────────────────────────────────────────────

    public async Task<CrossOrgTicketDto> ApproveAsync(
        Guid ticketId, Guid creatorOrgId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        // Only OrgTaskManager of creator org can approve
        if (!perms.CanBypassWorkflow(user, creatorOrgId))
            throw new UnauthorizedAccessException("Only OrgTaskManagers of the creator organization can approve.");

        var ticket = await LoadCrossOrgTicketAsync(ticketId, ct);
        if (ticket.CreatorOrgId != creatorOrgId)
            throw new UnauthorizedAccessException("You are not the creator organization for this ticket.");

        if (ticket.InternalMirrorIssueId is null)
            throw new InvalidOperationException("Mirror issue has not been created yet. Ticket must reach Done first.");

        // Move mirror issue to Done
        var mirrorIssue = await db.Issues
            .Include(i => i.Space).ThenInclude(s => s.WorkflowStatuses)
            .FirstOrDefaultAsync(i => i.Id == ticket.InternalMirrorIssueId, ct)
            ?? throw new KeyNotFoundException("Mirror issue not found.");

        var doneStatus = mirrorIssue.Space.WorkflowStatuses.FirstOrDefault(w => w.IsTerminal)
            ?? throw new InvalidOperationException("Internal space has no terminal status.");

        mirrorIssue.StatusId  = doneStatus.Id;
        mirrorIssue.UpdatedAt = DateTime.UtcNow;

        // Soft-close the external issue
        var externalIssue = await db.Issues.FindAsync([ticket.ExternalIssueId], ct)!;
        externalIssue!.DeletedAt  = DateTime.UtcNow;
        externalIssue.UpdatedAt   = DateTime.UtcNow;

        ticket.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        await hub.Clients.Group($"org:{ticket.ReceiverOrgId}")
            .SendAsync("external_ticket_approved", new { ticketId }, ct);

        return await LoadDtoAsync(ticketId, ct);
    }

    // ── Step 6b — Reject ──────────────────────────────────────────────────────

    public async Task<CrossOrgTicketDto> RejectAsync(
        Guid ticketId, Guid creatorOrgId, RejectTicketRequest req,
        ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (!perms.CanBypassWorkflow(user, creatorOrgId))
            throw new UnauthorizedAccessException("Only OrgTaskManagers of the creator organization can reject.");

        var ticket = await LoadCrossOrgTicketAsync(ticketId, ct);
        if (ticket.CreatorOrgId != creatorOrgId)
            throw new UnauthorizedAccessException("You are not the creator organization for this ticket.");

        if (ticket.InternalMirrorIssueId is null)
            throw new InvalidOperationException("Mirror issue has not been created yet.");

        // Post rejection comment on the mirror issue
        var callerMember = await db.OrgMembers
            .FirstOrDefaultAsync(m => m.OrgId == creatorOrgId && m.UserId == user.GetUserId(), ct)
            ?? throw new UnauthorizedAccessException("Caller is not a member of the creator organization.");

        db.Comments.Add(new Comment
        {
            IssueId     = ticket.InternalMirrorIssueId.Value,
            AuthorId    = user.GetUserId(),
            AuthorOrgId = creatorOrgId,
            Body        = $"[REJECTION] {req.Comment}"
        });

        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await hub.Clients.Group($"org:{ticket.ReceiverOrgId}")
            .SendAsync("external_ticket_rejected", new { ticketId, comment = req.Comment }, ct);

        return await LoadDtoAsync(ticketId, ct);
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<List<CrossOrgTicketDto>> GetSentAsync(Guid creatorOrgId, CancellationToken ct = default)
    {
        var tickets = await db.CrossOrgTickets
            .Include(t => t.CreatorOrg)
            .Include(t => t.ReceiverOrg)
            .Include(t => t.ExternalIssue).ThenInclude(i => i.Status)
            .Include(t => t.ExternalIssue).ThenInclude(i => i.Assignees).ThenInclude(a => a.User)
            .Where(t => t.CreatorOrgId == creatorOrgId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return tickets.Select(ToDto).ToList();
    }

    public async Task<List<CrossOrgTicketDto>> GetReceivedAsync(Guid receiverOrgId, CancellationToken ct = default)
    {
        var tickets = await db.CrossOrgTickets
            .Include(t => t.CreatorOrg)
            .Include(t => t.ReceiverOrg)
            .Include(t => t.ExternalIssue).ThenInclude(i => i.Status)
            .Include(t => t.ExternalIssue).ThenInclude(i => i.Assignees).ThenInclude(a => a.User)
            .Where(t => t.ReceiverOrgId == receiverOrgId && t.ExternalIssue.DeletedAt == null)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return tickets.Select(ToDto).ToList();
    }

    // ── Step 5 private: create mirror issue in creator org's Internal Space ───

    private async Task CreateMirrorIssueAsync(
        CrossOrgTicket ticket, Issue externalIssue, CancellationToken ct)
    {
        // Error handling: creator org deleted mid-lifecycle
        var creatorOrg = await db.Organizations.FindAsync([ticket.CreatorOrgId], ct);
        if (creatorOrg is null || !creatorOrg.IsActive)
        {
            logger.LogWarning(
                "CrossOrgTicket {TicketId}: creator org {OrgId} is inactive; skipping mirror creation.",
                ticket.Id, ticket.CreatorOrgId);
            ticket.IsCreatorOrgDeleted = true;
            return;
        }

        var internalSpace = await db.Spaces
            .Include(s => s.WorkflowStatuses)
            .FirstOrDefaultAsync(
                s => s.OrgId == ticket.CreatorOrgId &&
                     s.Type == SpaceType.Internal &&
                     s.Status == SpaceStatus.Active, ct);

        if (internalSpace is null)
        {
            logger.LogWarning(
                "CrossOrgTicket {TicketId}: creator org {OrgId} has no active internal space; skipping mirror.",
                ticket.Id, ticket.CreatorOrgId);
            ticket.IsCreatorOrgDeleted = true; // treat as non-actionable
            return;
        }

        var inReviewStatus = internalSpace.WorkflowStatuses
            .FirstOrDefault(w => w.Name == "In Review")
            ?? internalSpace.WorkflowStatuses.OrderBy(w => w.Position).Last();

        internalSpace.IssueCounter++;
        internalSpace.UpdatedAt = DateTime.UtcNow;

        var mirror = new Issue
        {
            SpaceId             = internalSpace.Id,
            OrgId               = ticket.CreatorOrgId,
            KeyNumber           = internalSpace.IssueCounter,
            Title               = externalIssue.Title,
            Description         = externalIssue.Description,
            Type                = IssueType.Task,
            Priority            = externalIssue.Priority,
            StatusId            = inReviewStatus.Id,
            ReporterId          = ticket.CreatedById,
            FileAttachmentsJson = externalIssue.FileAttachmentsJson
        };
        db.Issues.Add(mirror);

        ticket.InternalMirrorIssueId = mirror.Id;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<(CrossOrgTicket ticket, Issue externalIssue)> LoadReceiverTicketAsync(
        Guid ticketId, Guid receiverOrgId, CancellationToken ct)
    {
        var ticket = await LoadCrossOrgTicketAsync(ticketId, ct);
        if (ticket.ReceiverOrgId != receiverOrgId)
            throw new UnauthorizedAccessException("You are not the receiver organization for this ticket.");

        var externalIssue = await db.Issues.FindAsync([ticket.ExternalIssueId], ct)
            ?? throw new KeyNotFoundException("External issue not found.");

        if (externalIssue.DeletedAt != null)
            throw new InvalidOperationException("This ticket has been closed.");

        return (ticket, externalIssue);
    }

    private async Task<CrossOrgTicket> LoadCrossOrgTicketAsync(Guid ticketId, CancellationToken ct) =>
        await db.CrossOrgTickets.FindAsync([ticketId], ct)
            ?? throw new KeyNotFoundException("Cross-org ticket not found.");

    private async Task<CrossOrgTicketDto> LoadDtoAsync(Guid ticketId, CancellationToken ct) =>
        ToDto(await db.CrossOrgTickets
            .Include(t => t.CreatorOrg)
            .Include(t => t.ReceiverOrg)
            .Include(t => t.ExternalIssue).ThenInclude(i => i.Status)
            .Include(t => t.ExternalIssue).ThenInclude(i => i.Assignees).ThenInclude(a => a.User)
            .FirstAsync(t => t.Id == ticketId, ct));

    private static CrossOrgTicketDto ToDto(CrossOrgTicket t)
    {
        var assignee = t.ExternalIssue.Assignees.FirstOrDefault();
        return new CrossOrgTicketDto(
            t.Id,
            t.CreatorOrgId,
            t.CreatorOrg.Name,
            t.ReceiverOrgId,
            t.ReceiverOrg.Name,
            t.ExternalIssueId,
            t.InternalMirrorIssueId,
            t.ExternalIssue.Status.Name,
            t.ExternalIssue.Title,
            t.ExternalIssue.Priority.ToString(),
            t.ExternalIssue.Description,
            DeserializeAttachments(t.ExternalIssue.FileAttachmentsJson),
            assignee?.UserId,
            assignee?.User.FullName,
            t.ExternalIssue.StoryPoints,
            t.CompletionNotes,
            t.IsCreatorOrgDeleted,
            t.CreatedAt,
            t.UpdatedAt
        );
    }

    private static string? SerializeAttachments(List<FileAttachmentDto>? attachments) =>
        attachments is { Count: > 0 }
            ? JsonSerializer.Serialize(attachments, _json)
            : null;

    private static List<FileAttachmentDto> DeserializeAttachments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<FileAttachmentDto>>(json, _json) ?? []; }
        catch { return []; }
    }

    private static void RequirePermission(bool allowed, string message = "You do not have permission to perform this action.")
    {
        if (!allowed) throw new UnauthorizedAccessException(message);
    }
}
