using System.Security.Claims;

namespace Edcom.Api.Modules.Authorization.Services;

/// <summary>
/// Stateless permission-check service.  All methods inspect JWT claims only —
/// no database round-trips — so they can be called freely in hot paths.
/// </summary>
public interface IPermissionService
{
    // ── Organisation management ───────────────────────────────────────────────

    /// <summary>Create org: Admin only.</summary>
    bool CanCreateOrg(ClaimsPrincipal user);

    /// <summary>Delete org: Admin only.</summary>
    bool CanDeleteOrg(ClaimsPrincipal user);

    /// <summary>Invite / remove member, assign roles: Admin OR OrgTaskManager of that org.</summary>
    bool CanManageMembers(ClaimsPrincipal user, Guid orgId);

    /// <summary>View member list: Admin, OrgTaskManager, Employer of that org (not Viewer).</summary>
    bool CanViewOrgMembers(ClaimsPrincipal user, Guid orgId);

    // ── Internal space ────────────────────────────────────────────────────────

    /// <summary>Create / edit / delete tickets: Admin, OrgTaskManager, Employer.</summary>
    bool CanWriteTicket(ClaimsPrincipal user, Guid orgId);

    /// <summary>Change ticket status (any role that is allowed to touch status).</summary>
    bool CanChangeStatus(ClaimsPrincipal user, Guid orgId);

    /// <summary>
    /// Bypass workflow transition rules (jump to any status).
    /// Admin and OrgTaskManager only; Employers must follow transition graph.
    /// </summary>
    bool CanBypassWorkflow(ClaimsPrincipal user, Guid orgId);

    /// <summary>Configure workflow statuses / transitions: Admin, OrgTaskManager.</summary>
    bool CanConfigureWorkflow(ClaimsPrincipal user, Guid orgId);

    /// <summary>Create / start / complete sprint: Admin, OrgTaskManager.</summary>
    bool CanManageSprints(ClaimsPrincipal user, Guid orgId);

    /// <summary>Create Epic: Admin, OrgTaskManager, Employer.</summary>
    bool CanCreateEpic(ClaimsPrincipal user, Guid orgId);

    // ── External space (receiver-org perspective) ─────────────────────────────

    /// <summary>View incoming tickets: OrgTaskManager, Employer of receiver org.</summary>
    bool CanViewIncomingTickets(ClaimsPrincipal user, Guid receiverOrgId);

    /// <summary>Assign employee, set estimation / due date: OrgTaskManager only.</summary>
    bool CanAssignAndEstimate(ClaimsPrincipal user, Guid receiverOrgId);

    /// <summary>Move Backlog → To Do: OrgTaskManager only.</summary>
    bool CanMoveToDo(ClaimsPrincipal user, Guid receiverOrgId);

    /// <summary>Progress ticket (To Do → Done per workflow): OrgTaskManager, Employer.</summary>
    bool CanProgressExternalTicket(ClaimsPrincipal user, Guid receiverOrgId);

    // ── Cross-org ticket (creator-org perspective) ───────────────────────────

    /// <summary>Send a cross-org ticket to another org: OrgTaskManager, Employer.</summary>
    bool CanSendCrossOrgTicket(ClaimsPrincipal user, Guid creatorOrgId);

    /// <summary>View ticket status / comment on it: any member of creator org.</summary>
    bool CanViewOrCommentOnCrossOrgTicket(ClaimsPrincipal user, Guid creatorOrgId);

    /// <summary>
    /// Edit fields on the external (receiver-side) ticket.
    /// Always <c>false</c> for members of the creator org (regardless of role).
    /// System Admins are the only exception.
    /// </summary>
    bool CanEditExternalTicketFields(ClaimsPrincipal user, Guid creatorOrgId);

    // ── Dashboard ─────────────────────────────────────────────────────────────

    DashboardView GetDashboardView(ClaimsPrincipal user);
}

/// <summary>What level of dashboard data a user may see.</summary>
public enum DashboardView
{
    /// <summary>All-org statistics (Admin only).</summary>
    SystemWide,
    /// <summary>Own org statistics + cross-org in-progress (OrgTaskManager).</summary>
    OrgManager,
    /// <summary>My tasks and deadlines (Employer).</summary>
    MyTasks,
    /// <summary>Read-only board summaries (Viewer).</summary>
    ReadOnly,
    /// <summary>Not authenticated or no role assigned.</summary>
    None
}
