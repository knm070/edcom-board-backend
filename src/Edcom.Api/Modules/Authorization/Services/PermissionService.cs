using System.Security.Claims;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Authorization.Extensions;

namespace Edcom.Api.Modules.Authorization.Services;

/// <inheritdoc/>
public sealed class PermissionService : IPermissionService
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool AdminOrManager(ClaimsPrincipal u, Guid orgId)
        => u.HasOrgRole(orgId, MemberRole.OrgTaskManager); // IsSystemAdmin baked in

    private static bool AdminManagerOrEmployer(ClaimsPrincipal u, Guid orgId)
        => u.HasOrgRole(orgId, MemberRole.OrgTaskManager, MemberRole.Employer);

    // ── Organisation management ───────────────────────────────────────────────

    public bool CanCreateOrg(ClaimsPrincipal user) => user.IsSystemAdmin();
    public bool CanDeleteOrg(ClaimsPrincipal user) => user.IsSystemAdmin();

    public bool CanManageMembers(ClaimsPrincipal user, Guid orgId)
        => AdminOrManager(user, orgId);

    public bool CanViewOrgMembers(ClaimsPrincipal user, Guid orgId)
        => AdminManagerOrEmployer(user, orgId); // Viewer excluded per spec

    // ── Internal space ────────────────────────────────────────────────────────

    public bool CanWriteTicket(ClaimsPrincipal user, Guid orgId)
        => AdminManagerOrEmployer(user, orgId);

    public bool CanChangeStatus(ClaimsPrincipal user, Guid orgId)
        => AdminManagerOrEmployer(user, orgId);

    public bool CanBypassWorkflow(ClaimsPrincipal user, Guid orgId)
        => AdminOrManager(user, orgId);

    public bool CanConfigureWorkflow(ClaimsPrincipal user, Guid orgId)
        => AdminOrManager(user, orgId);

    public bool CanManageSprints(ClaimsPrincipal user, Guid orgId)
        => AdminOrManager(user, orgId);

    public bool CanCreateEpic(ClaimsPrincipal user, Guid orgId)
        => AdminManagerOrEmployer(user, orgId);

    // ── External space ────────────────────────────────────────────────────────

    public bool CanViewIncomingTickets(ClaimsPrincipal user, Guid receiverOrgId)
        => AdminManagerOrEmployer(user, receiverOrgId);

    public bool CanAssignAndEstimate(ClaimsPrincipal user, Guid receiverOrgId)
        => AdminOrManager(user, receiverOrgId);

    public bool CanMoveToDo(ClaimsPrincipal user, Guid receiverOrgId)
        => AdminOrManager(user, receiverOrgId);

    public bool CanProgressExternalTicket(ClaimsPrincipal user, Guid receiverOrgId)
        => AdminManagerOrEmployer(user, receiverOrgId);

    // ── Cross-org ─────────────────────────────────────────────────────────────

    public bool CanSendCrossOrgTicket(ClaimsPrincipal user, Guid creatorOrgId)
        => AdminManagerOrEmployer(user, creatorOrgId);

    public bool CanViewOrCommentOnCrossOrgTicket(ClaimsPrincipal user, Guid creatorOrgId)
        => user.IsMemberOfOrg(creatorOrgId);

    /// <summary>
    /// Creator-org members are FORBIDDEN from editing fields on the receiver side,
    /// regardless of their role.  Only a system Admin may override this rule.
    /// </summary>
    public bool CanEditExternalTicketFields(ClaimsPrincipal user, Guid creatorOrgId)
    {
        // System Admin can always override
        if (user.IsSystemAdmin()) return true;

        // Anyone who belongs to the creator org is blocked — even OrgTaskManager
        return !user.IsMemberOfOrg(creatorOrgId);
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    public DashboardView GetDashboardView(ClaimsPrincipal user)
    {
        if (user.IsSystemAdmin())
            return DashboardView.SystemWide;

        if (user.HasAnyOrgRole(MemberRole.OrgTaskManager))
            return DashboardView.OrgManager;

        if (user.HasAnyOrgRole(MemberRole.Employer))
            return DashboardView.MyTasks;

        if (user.HasAnyOrgRole(MemberRole.Viewer))
            return DashboardView.ReadOnly;

        return DashboardView.None;
    }
}
