using System.Security.Claims;

namespace Edcom.Api.Modules.Authorization.Services;

/// <summary>
/// Stateless permission-check service. Inspects JWT claims only — no DB round-trips.
/// </summary>
public interface IPermissionService
{
    // ── Organisation management ───────────────────────────────────────────────

    bool CanRequestOrg(ClaimsPrincipal user);
    bool CanApproveOrg(ClaimsPrincipal user);
    bool CanManageMembers(ClaimsPrincipal user, Guid orgId);
    bool CanViewOrgMembers(ClaimsPrincipal user, Guid orgId);

    // ── Space management ──────────────────────────────────────────────────────

    bool CanCreateSpace(ClaimsPrincipal user, Guid orgId);
    bool CanManageSpace(ClaimsPrincipal user, Guid orgId, Guid spaceId);

    // ── Ticket permissions ────────────────────────────────────────────────────

    bool CanWriteTicket(ClaimsPrincipal user, Guid orgId);
    bool CanChangeStatus(ClaimsPrincipal user, Guid orgId);
    bool CanBypassWorkflow(ClaimsPrincipal user, Guid orgId);
    bool CanConfigureWorkflow(ClaimsPrincipal user, Guid orgId, Guid spaceId);
    bool CanManageSprints(ClaimsPrincipal user, Guid orgId, Guid spaceId);
    bool CanCreateEpic(ClaimsPrincipal user, Guid orgId);

    // ── Dashboard ─────────────────────────────────────────────────────────────

    DashboardView GetDashboardView(ClaimsPrincipal user);
}

/// <summary>What level of dashboard data a user may see.</summary>
public enum DashboardView
{
    SystemWide,   // Admin only
    OrgManager,   // OrgManager
    SpaceManager, // SpaceManager
    MyTasks,      // Employer
    None
}
