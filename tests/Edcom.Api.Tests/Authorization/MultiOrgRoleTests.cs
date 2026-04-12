using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Authorization.Services;

namespace Edcom.Api.Tests.Authorization;

/// <summary>
/// Verifies that a single user can hold DIFFERENT roles in different orgs
/// and that the permission checks are correctly scoped per-org.
/// </summary>
public sealed class MultiOrgRoleTests
{
    private readonly IPermissionService _svc = new PermissionService();

    private static readonly Guid OrgA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid OrgB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    /// <summary>
    /// User is OrgTaskManager in Org-A and Employer in Org-B.
    /// - In Org-A they can bypass the workflow (manager privilege).
    /// - In Org-B they CANNOT bypass the workflow (employer only).
    /// </summary>
    [Fact]
    public void ManagerInOrgA_EmployerInOrgB_WorkflowBypass_IsOrgScoped()
    {
        var user = new ClaimsPrincipalBuilder()
            .WithOrgRole(OrgA, MemberRole.OrgTaskManager)
            .WithOrgRole(OrgB, MemberRole.Employer)
            .Build();

        Assert.True(_svc.CanBypassWorkflow(user, OrgA),
            "OrgTaskManager in Org-A should bypass workflow in Org-A.");
        Assert.False(_svc.CanBypassWorkflow(user, OrgB),
            "Employer in Org-B must NOT bypass workflow in Org-B.");
    }

    /// <summary>
    /// Manager in Org-A can manage members of Org-A, but NOT of Org-B where they are only an Employer.
    /// </summary>
    [Fact]
    public void ManagerInOrgA_CanManageMembers_OnlyInOrgA()
    {
        var user = new ClaimsPrincipalBuilder()
            .WithOrgRole(OrgA, MemberRole.OrgTaskManager)
            .WithOrgRole(OrgB, MemberRole.Employer)
            .Build();

        Assert.True(_svc.CanManageMembers(user, OrgA));
        Assert.False(_svc.CanManageMembers(user, OrgB));
    }

    /// <summary>
    /// Employer in Org-A can write tickets in Org-A but has NO access at all in Org-C (not a member).
    /// </summary>
    [Fact]
    public void EmployerInOrgA_CannotWriteTicket_InUnknownOrg()
    {
        var orgC = Guid.NewGuid();
        var user = new ClaimsPrincipalBuilder()
            .WithOrgRole(OrgA, MemberRole.Employer)
            .Build();

        Assert.True(_svc.CanWriteTicket(user, OrgA));
        Assert.False(_svc.CanWriteTicket(user, orgC));
    }

    /// <summary>
    /// Dashboard view priority: when a user is OrgTaskManager in one org and Employer in another,
    /// the OrgManager view is returned (higher-priority role wins).
    /// </summary>
    [Fact]
    public void DashboardView_PrioritisesHigherRole_WhenMultipleOrgs()
    {
        var user = new ClaimsPrincipalBuilder()
            .WithOrgRole(OrgA, MemberRole.OrgTaskManager)
            .WithOrgRole(OrgB, MemberRole.Employer)
            .Build();

        Assert.Equal(DashboardView.OrgManager, _svc.GetDashboardView(user));
    }

    /// <summary>
    /// System Admin reports SystemWide dashboard regardless of any org memberships.
    /// </summary>
    [Fact]
    public void SystemAdmin_AlwaysGetsSystemWideDashboard()
    {
        var admin = new ClaimsPrincipalBuilder()
            .AsSystemAdmin()
            .WithOrgRole(OrgA, MemberRole.Employer)  // lower role present — should be ignored
            .Build();

        Assert.Equal(DashboardView.SystemWide, _svc.GetDashboardView(admin));
    }

    /// <summary>
    /// System Admin implicitly satisfies all org-scoped permission checks for any orgId.
    /// </summary>
    [Fact]
    public void SystemAdmin_PassesAllOrgChecks_ForAnyOrg()
    {
        var unknownOrg = Guid.NewGuid();
        var admin = new ClaimsPrincipalBuilder().AsSystemAdmin().Build();

        Assert.True(_svc.CanManageMembers(admin, unknownOrg));
        Assert.True(_svc.CanWriteTicket(admin, unknownOrg));
        Assert.True(_svc.CanBypassWorkflow(admin, unknownOrg));
        Assert.True(_svc.CanConfigureWorkflow(admin, unknownOrg));
        Assert.True(_svc.CanManageSprints(admin, unknownOrg));
    }
}
