using System.Security.Claims;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Authorization.Extensions;

namespace Edcom.Api.Modules.Authorization.Services;

public sealed class PermissionService : IPermissionService
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsOrgManager(ClaimsPrincipal u, Guid orgId)
        => u.HasOrgRole(orgId, OrgRole.OrgManager);

    private static bool IsSpaceManagerOrAbove(ClaimsPrincipal u, Guid orgId, Guid spaceId)
        => u.IsSpaceManagerOrAbove(orgId, spaceId);

    private static bool IsOrgMember(ClaimsPrincipal u, Guid orgId)
        => u.IsMemberOfOrg(orgId);

    // ── Organisation management ───────────────────────────────────────────────

    public bool CanRequestOrg(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;
    public bool CanApproveOrg(ClaimsPrincipal user) => user.IsSystemAdmin();

    public bool CanManageMembers(ClaimsPrincipal user, Guid orgId)
        => IsOrgManager(user, orgId);

    public bool CanViewOrgMembers(ClaimsPrincipal user, Guid orgId)
        => IsOrgMember(user, orgId);

    // ── Space management ──────────────────────────────────────────────────────

    public bool CanCreateSpace(ClaimsPrincipal user, Guid orgId)
        => IsOrgManager(user, orgId);

    public bool CanManageSpace(ClaimsPrincipal user, Guid orgId, Guid spaceId)
        => IsSpaceManagerOrAbove(user, orgId, spaceId);

    // ── Ticket permissions ────────────────────────────────────────────────────

    public bool CanWriteTicket(ClaimsPrincipal user, Guid orgId)
        => IsOrgMember(user, orgId) && !user.IsSystemAdmin();

    public bool CanChangeStatus(ClaimsPrincipal user, Guid orgId)
        => IsOrgMember(user, orgId) && !user.IsSystemAdmin();

    /// <summary>OrgManager only — full override (jump to any status).</summary>
    public bool CanBypassWorkflow(ClaimsPrincipal user, Guid orgId)
        => IsOrgManager(user, orgId);

    public bool CanConfigureWorkflow(ClaimsPrincipal user, Guid orgId, Guid spaceId)
        => IsSpaceManagerOrAbove(user, orgId, spaceId);

    public bool CanManageSprints(ClaimsPrincipal user, Guid orgId, Guid spaceId)
        => IsSpaceManagerOrAbove(user, orgId, spaceId);

    public bool CanCreateEpic(ClaimsPrincipal user, Guid orgId)
        => IsOrgMember(user, orgId) && !user.IsSystemAdmin();

    // ── Dashboard ─────────────────────────────────────────────────────────────

    public DashboardView GetDashboardView(ClaimsPrincipal user)
    {
        if (user.IsSystemAdmin()) return DashboardView.SystemWide;
        if (user.HasAnyOrgRole(OrgRole.OrgManager))    return DashboardView.OrgManager;
        if (user.HasAnyOrgRole(OrgRole.SpaceManager))  return DashboardView.SpaceManager;
        if (user.HasAnyOrgRole(OrgRole.Employer))      return DashboardView.MyTasks;
        return DashboardView.None;
    }
}
