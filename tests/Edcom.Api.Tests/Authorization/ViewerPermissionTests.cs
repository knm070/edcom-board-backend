using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Authorization.Services;

namespace Edcom.Api.Tests.Authorization;

/// <summary>
/// Verifies that a Viewer role is read-only:
///   - Cannot write tickets
///   - Cannot change status
///   - Cannot manage members
///   - Cannot view member list (per spec: Viewer excluded from CanViewOrgMembers)
///   - Cannot bypass workflow
///   - Cannot manage sprints or configure workflow
///   - Gets the ReadOnly dashboard view
/// </summary>
public sealed class ViewerPermissionTests
{
    private readonly IPermissionService _svc = new PermissionService();

    private static readonly Guid OrgId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");

    private static ClaimsPrincipalBuilder ViewerIn(Guid orgId) =>
        new ClaimsPrincipalBuilder().WithOrgRole(orgId, MemberRole.Viewer);

    // ── Write operations ──────────────────────────────────────────────────────

    [Fact]
    public void Viewer_CannotWriteTicket()
    {
        var viewer = ViewerIn(OrgId).Build();
        Assert.False(_svc.CanWriteTicket(viewer, OrgId));
    }

    [Fact]
    public void Viewer_CannotChangeStatus()
    {
        var viewer = ViewerIn(OrgId).Build();
        Assert.False(_svc.CanChangeStatus(viewer, OrgId));
    }

    [Fact]
    public void Viewer_CannotManageMembers()
    {
        var viewer = ViewerIn(OrgId).Build();
        Assert.False(_svc.CanManageMembers(viewer, OrgId));
    }

    [Fact]
    public void Viewer_CannotViewMemberList()
    {
        // Spec: "Viewer excluded" from CanViewOrgMembers
        var viewer = ViewerIn(OrgId).Build();
        Assert.False(_svc.CanViewOrgMembers(viewer, OrgId));
    }

    [Fact]
    public void Viewer_CannotBypassWorkflow()
    {
        var viewer = ViewerIn(OrgId).Build();
        Assert.False(_svc.CanBypassWorkflow(viewer, OrgId));
    }

    [Fact]
    public void Viewer_CannotConfigureWorkflow()
    {
        var viewer = ViewerIn(OrgId).Build();
        Assert.False(_svc.CanConfigureWorkflow(viewer, OrgId));
    }

    [Fact]
    public void Viewer_CannotManageSprints()
    {
        var viewer = ViewerIn(OrgId).Build();
        Assert.False(_svc.CanManageSprints(viewer, OrgId));
    }

    [Fact]
    public void Viewer_CannotCreateEpic()
    {
        var viewer = ViewerIn(OrgId).Build();
        Assert.False(_svc.CanCreateEpic(viewer, OrgId));
    }

    // ── Cross-org ─────────────────────────────────────────────────────────────

    [Fact]
    public void Viewer_CannotSendCrossOrgTicket()
    {
        var viewer = ViewerIn(OrgId).Build();
        Assert.False(_svc.CanSendCrossOrgTicket(viewer, OrgId));
    }

    [Fact]
    public void Viewer_CanViewAndCommentOnCrossOrgTickets_AsOrgMember()
    {
        // Viewers can still READ cross-org tickets in their org
        var viewer = ViewerIn(OrgId).Build();
        Assert.True(_svc.CanViewOrCommentOnCrossOrgTicket(viewer, OrgId));
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    [Fact]
    public void Viewer_GetsReadOnlyDashboard()
    {
        var viewer = ViewerIn(OrgId).Build();
        Assert.Equal(DashboardView.ReadOnly, _svc.GetDashboardView(viewer));
    }

    // ── Unauthenticated / no-org user ─────────────────────────────────────────

    [Fact]
    public void UserWithNoOrgMembership_GetsNoneDashboard()
    {
        var nobody = new ClaimsPrincipalBuilder().Build();
        Assert.Equal(DashboardView.None, _svc.GetDashboardView(nobody));
    }

    [Fact]
    public void UserWithNoOrgMembership_CannotWriteInAnyOrg()
    {
        var nobody = new ClaimsPrincipalBuilder().Build();
        Assert.False(_svc.CanWriteTicket(nobody, OrgId));
        Assert.False(_svc.CanChangeStatus(nobody, OrgId));
        Assert.False(_svc.CanManageMembers(nobody, OrgId));
    }
}
