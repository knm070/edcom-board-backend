using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Authorization.Services;

namespace Edcom.Api.Tests.Authorization;

/// <summary>
/// Verifies the cross-org field-edit rule:
///   "Members of the creator-org CANNOT edit external ticket fields,
///    even if they are an OrgTaskManager.  Only a System Admin may override."
/// </summary>
public sealed class CrossOrgFieldEditTests
{
    private readonly IPermissionService _svc = new PermissionService();

    private static readonly Guid CreatorOrg   = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid ReceiverOrg  = Guid.Parse("dddddddd-0000-0000-0000-000000000002");

    /// <summary>
    /// An OrgTaskManager in the CREATOR org is forbidden from editing
    /// the external ticket's fields (receiver-side fields).
    /// </summary>
    [Fact]
    public void OrgTaskManager_InCreatorOrg_CannotEditExternalFields()
    {
        var user = new ClaimsPrincipalBuilder()
            .WithOrgRole(CreatorOrg, MemberRole.OrgTaskManager)
            .Build();

        Assert.False(_svc.CanEditExternalTicketFields(user, CreatorOrg),
            "OrgTaskManager in the creator org must NOT edit external ticket fields.");
    }

    /// <summary>
    /// A regular Employer in the CREATOR org is also forbidden.
    /// </summary>
    [Fact]
    public void Employer_InCreatorOrg_CannotEditExternalFields()
    {
        var user = new ClaimsPrincipalBuilder()
            .WithOrgRole(CreatorOrg, MemberRole.Employer)
            .Build();

        Assert.False(_svc.CanEditExternalTicketFields(user, CreatorOrg));
    }

    /// <summary>
    /// A user who belongs only to the RECEIVER org (not the creator org) CAN edit
    /// the external ticket fields.
    /// </summary>
    [Fact]
    public void Employer_InReceiverOrg_CanEditExternalFields()
    {
        var user = new ClaimsPrincipalBuilder()
            .WithOrgRole(ReceiverOrg, MemberRole.Employer)
            .Build();

        Assert.True(_svc.CanEditExternalTicketFields(user, CreatorOrg),
            "Receiver-org member is not in the creator org so they should be allowed.");
    }

    /// <summary>
    /// A System Admin can edit external ticket fields regardless of org membership.
    /// </summary>
    [Fact]
    public void SystemAdmin_CanAlwaysEditExternalFields()
    {
        // Admin who also happens to be a member of the creator org
        var admin = new ClaimsPrincipalBuilder()
            .AsSystemAdmin()
            .WithOrgRole(CreatorOrg, MemberRole.OrgTaskManager)
            .Build();

        Assert.True(_svc.CanEditExternalTicketFields(admin, CreatorOrg),
            "System Admin override must be respected even for creator-org membership.");
    }

    /// <summary>
    /// A user in BOTH the creator and receiver orgs (e.g. a consultant) is blocked
    /// because their creator-org membership triggers the prohibition.
    /// </summary>
    [Fact]
    public void UserInBothOrgs_IsBlocked_BecauseOfCreatorOrgMembership()
    {
        var user = new ClaimsPrincipalBuilder()
            .WithOrgRole(CreatorOrg,  MemberRole.OrgTaskManager)
            .WithOrgRole(ReceiverOrg, MemberRole.OrgTaskManager)
            .Build();

        Assert.False(_svc.CanEditExternalTicketFields(user, CreatorOrg),
            "Being in the receiver org doesn't lift the creator-org prohibition.");
    }

    /// <summary>
    /// A completely unrelated user (member of neither org) can also edit external fields —
    /// they are not in the creator org, so the prohibition doesn't apply.
    /// Note: in practice the controller will gate them earlier via space-membership checks;
    /// this test confirms the permission rule itself is not over-restrictive.
    /// </summary>
    [Fact]
    public void UnrelatedUser_IsAllowed_ByFieldEditRule()
    {
        var unrelated = new ClaimsPrincipalBuilder().Build();   // no org memberships

        Assert.True(_svc.CanEditExternalTicketFields(unrelated, CreatorOrg));
    }
}
