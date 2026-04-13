using Edcom.Api.Modules.Authorization.Extensions;
using Edcom.Api.Modules.CrossOrgTickets.Dto;
using Edcom.Api.Modules.CrossOrgTickets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edcom.Api.Modules.CrossOrgTickets.Controllers;

/// <summary>
/// Handles the 6-step cross-organisation ticket lifecycle.
///
/// Creator-org routes  → /api/v1/orgs/{orgId}/outbox/tickets
/// Receiver-org routes → /api/v1/orgs/{orgId}/inbox/tickets
/// </summary>
[ApiController]
[Authorize]
public class CrossOrgTicketsController(ICrossOrgTicketService service) : ControllerBase
{
    // ── Creator-org: send ─────────────────────────────────────────────────────

    /// <summary>Step 1 — Employer or OrgTaskManager sends a ticket to another org.</summary>
    [HttpPost("api/v1/orgs/{orgId:guid}/outbox/tickets")]
    public async Task<ActionResult<CrossOrgTicketDto>> Send(
        Guid orgId, [FromBody] SendCrossOrgTicketRequest req, CancellationToken ct)
    {
        var result = await service.SendAsync(orgId, req, User, ct);
        return CreatedAtAction(nameof(GetSent), new { orgId }, result);
    }

    /// <summary>GET all tickets sent by this org.</summary>
    [HttpGet("api/v1/orgs/{orgId:guid}/outbox/tickets")]
    public async Task<ActionResult<List<CrossOrgTicketDto>>> GetSent(Guid orgId, CancellationToken ct)
    {
        RequireMembership(orgId);
        return await service.GetSentAsync(orgId, ct);
    }

    // ── Creator-org: review actions ───────────────────────────────────────────

    /// <summary>Step 6a — Creator OrgTaskManager approves the completed work.</summary>
    [HttpPost("api/v1/orgs/{orgId:guid}/outbox/tickets/{ticketId:guid}/approve")]
    public async Task<ActionResult<CrossOrgTicketDto>> Approve(
        Guid orgId, Guid ticketId, CancellationToken ct)
    {
        var result = await service.ApproveAsync(ticketId, orgId, User, ct);
        return Ok(result);
    }

    /// <summary>Step 6b — Creator OrgTaskManager rejects and posts a rejection comment.</summary>
    [HttpPost("api/v1/orgs/{orgId:guid}/outbox/tickets/{ticketId:guid}/reject")]
    public async Task<ActionResult<CrossOrgTicketDto>> Reject(
        Guid orgId, Guid ticketId, [FromBody] RejectTicketRequest req, CancellationToken ct)
    {
        var result = await service.RejectAsync(ticketId, orgId, req, User, ct);
        return Ok(result);
    }

    // ── Receiver-org: inbox ───────────────────────────────────────────────────

    /// <summary>GET all tickets received by this org (excludes closed).</summary>
    [HttpGet("api/v1/orgs/{orgId:guid}/inbox/tickets")]
    public async Task<ActionResult<List<CrossOrgTicketDto>>> GetReceived(Guid orgId, CancellationToken ct)
    {
        RequireMembership(orgId);
        return await service.GetReceivedAsync(orgId, ct);
    }

    /// <summary>Step 2 — Receiver OrgTaskManager assigns an employer and sets estimation/due date.</summary>
    [HttpPatch("api/v1/orgs/{orgId:guid}/inbox/tickets/{ticketId:guid}/assign")]
    public async Task<ActionResult<CrossOrgTicketDto>> Assign(
        Guid orgId, Guid ticketId, [FromBody] AssignTicketRequest req, CancellationToken ct)
    {
        var result = await service.AssignAsync(ticketId, orgId, req, User, ct);
        return Ok(result);
    }

    /// <summary>Step 3 — Receiver OrgTaskManager activates the ticket (Backlog → To Do).</summary>
    [HttpPost("api/v1/orgs/{orgId:guid}/inbox/tickets/{ticketId:guid}/activate")]
    public async Task<ActionResult<CrossOrgTicketDto>> Activate(
        Guid orgId, Guid ticketId, CancellationToken ct)
    {
        var result = await service.ActivateAsync(ticketId, orgId, User, ct);
        return Ok(result);
    }

    /// <summary>
    /// Step 4 — Receiver progresses ticket through statuses.
    /// Step 5 auto-fires when TargetStatusName == "Done".
    /// </summary>
    [HttpPost("api/v1/orgs/{orgId:guid}/inbox/tickets/{ticketId:guid}/progress")]
    public async Task<ActionResult<CrossOrgTicketDto>> Progress(
        Guid orgId, Guid ticketId, [FromBody] ProgressTicketRequest req, CancellationToken ct)
    {
        var result = await service.ProgressAsync(ticketId, orgId, req, User, ct);
        return Ok(result);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private void RequireMembership(Guid orgId)
    {
        if (!User.IsMemberOfOrg(orgId))
            throw new UnauthorizedAccessException("You are not a member of this organization.");
    }
}
