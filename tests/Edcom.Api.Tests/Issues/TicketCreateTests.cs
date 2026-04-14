using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Infrastructure.Results;
using Edcom.Api.Modules.Issues.Dto;
using Edcom.Api.Tests.Authorization;

namespace Edcom.Api.Tests.Issues;

/// <summary>
/// Unit tests for TicketService.CreateAsync role-based business rules.
/// </summary>
public sealed class TicketCreateTests : TicketServiceTestBase
{
    private static CreateIssueRequest ValidReq(List<Guid>? assignees = null) => new(
        Title:       "Test ticket",
        Description: null,
        Type:        "Task",
        Priority:    "Medium",
        StatusId:    StatusTodoId,
        SprintId:    null,
        EpicId:      null,
        AssigneeIds: assignees,
        StoryPoints: null,
        DueDate:     null
    );

    // ── SystemAdmin ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SystemAdmin_CannotCreateTicket()
    {
        var admin = new ClaimsPrincipalBuilder().AsSystemAdmin().Build();

        var result = await Svc.CreateAsync(SpaceId, ValidReq(), admin, default);

        Assert.False(result.IsSuccess);
        Assert.IsType<Result.Unauthorized>(result.Outcome);
    }

    // ── Employer ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Employer_WithNoAssignees_AutoAssignsToSelf()
    {
        var user = new ClaimsPrincipalBuilder()
            .WithUserId(EmployerUserId)
            .WithOrgRole(OrgId, OrgRole.Employer)
            .Build();

        var result = await Svc.CreateAsync(SpaceId, ValidReq(), user, default);

        Assert.True(result.IsSuccess);
        var issue = await Db.Issues.FindAsync(result.Data!.Id);
        var assignees = Db.Set<IssueAssignee>().Where(a => a.IssueId == issue!.Id).ToList();
        Assert.Single(assignees);
        Assert.Equal(EmployerUserId, assignees[0].UserId);
    }

    [Fact]
    public async Task Employer_AssignToSelf_IsAllowed()
    {
        var user = new ClaimsPrincipalBuilder()
            .WithUserId(EmployerUserId)
            .WithOrgRole(OrgId, OrgRole.Employer)
            .Build();

        var result = await Svc.CreateAsync(SpaceId, ValidReq(assignees: [EmployerUserId]), user, default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Employer_AssignToOthers_IsRejected()
    {
        var user = new ClaimsPrincipalBuilder()
            .WithUserId(EmployerUserId)
            .WithOrgRole(OrgId, OrgRole.Employer)
            .Build();

        var result = await Svc.CreateAsync(SpaceId, ValidReq(assignees: [OtherUserId]), user, default);

        Assert.False(result.IsSuccess);
        var failure = Assert.IsType<Result.Failure>(result.Outcome);
        Assert.Equal("EMPLOYER_CANNOT_ASSIGN_OTHERS", failure.Code);
    }

    [Fact]
    public async Task Employer_AssignToSelfAndOthers_IsRejected()
    {
        var user = new ClaimsPrincipalBuilder()
            .WithUserId(EmployerUserId)
            .WithOrgRole(OrgId, OrgRole.Employer)
            .Build();

        var result = await Svc.CreateAsync(
            SpaceId,
            ValidReq(assignees: [EmployerUserId, OtherUserId]),
            user,
            default);

        Assert.False(result.IsSuccess);
        Assert.IsType<Result.Failure>(result.Outcome);
    }

    // ── OrgManager ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task OrgManager_CanAssignToAnyOrgMember()
    {
        var manager = new ClaimsPrincipalBuilder()
            .WithUserId(ManagerUserId)
            .WithOrgRole(OrgId, OrgRole.OrgManager)
            .Build();

        var result = await Svc.CreateAsync(SpaceId, ValidReq(assignees: [OtherUserId]), manager, default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task OrgManager_CanCreateWithNoAssignees()
    {
        var manager = new ClaimsPrincipalBuilder()
            .WithUserId(ManagerUserId)
            .WithOrgRole(OrgId, OrgRole.OrgManager)
            .Build();

        var result = await Svc.CreateAsync(SpaceId, ValidReq(), manager, default);

        Assert.True(result.IsSuccess);
    }

    // ── Validation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task InvalidType_ReturnsFailure()
    {
        var manager = new ClaimsPrincipalBuilder()
            .WithUserId(ManagerUserId)
            .WithOrgRole(OrgId, OrgRole.OrgManager)
            .Build();

        var req = ValidReq() with { Type = "InvalidType" };
        var result = await Svc.CreateAsync(SpaceId, req, manager, default);

        Assert.False(result.IsSuccess);
        var failure = Assert.IsType<Result.Failure>(result.Outcome);
        Assert.Equal("INVALID_TYPE", failure.Code);
    }

    [Fact]
    public async Task InvalidPriority_ReturnsFailure()
    {
        var manager = new ClaimsPrincipalBuilder()
            .WithUserId(ManagerUserId)
            .WithOrgRole(OrgId, OrgRole.OrgManager)
            .Build();

        var req = ValidReq() with { Priority = "UltraCritical" };
        var result = await Svc.CreateAsync(SpaceId, req, manager, default);

        Assert.False(result.IsSuccess);
        var failure = Assert.IsType<Result.Failure>(result.Outcome);
        Assert.Equal("INVALID_PRIORITY", failure.Code);
    }

    [Fact]
    public async Task NonMember_CannotCreate()
    {
        // User belongs to a completely different org
        var stranger = new ClaimsPrincipalBuilder()
            .WithUserId(Guid.NewGuid())
            .WithOrgRole(Guid.NewGuid(), OrgRole.OrgManager)
            .Build();

        var result = await Svc.CreateAsync(SpaceId, ValidReq(), stranger, default);

        Assert.False(result.IsSuccess);
        Assert.IsType<Result.Unauthorized>(result.Outcome);
    }

    [Fact]
    public async Task SpaceNotFound_ReturnsNotFound()
    {
        var manager = new ClaimsPrincipalBuilder()
            .WithUserId(ManagerUserId)
            .WithOrgRole(OrgId, OrgRole.OrgManager)
            .Build();

        var result = await Svc.CreateAsync(Guid.NewGuid(), ValidReq(), manager, default);

        Assert.False(result.IsSuccess);
        Assert.IsType<Result.NotFound>(result.Outcome);
    }

    [Fact]
    public async Task Create_IncrementsIssueCounter()
    {
        var manager = new ClaimsPrincipalBuilder()
            .WithUserId(ManagerUserId)
            .WithOrgRole(OrgId, OrgRole.OrgManager)
            .Build();

        var r1 = await Svc.CreateAsync(SpaceId, ValidReq(), manager, default);
        var r2 = await Svc.CreateAsync(SpaceId, ValidReq(), manager, default);

        Assert.True(r1.IsSuccess);
        Assert.True(r2.IsSuccess);
        // Each ticket should get a unique, incrementing key number
        Assert.NotEqual(r1.Data!.Key, r2.Data!.Key);
    }
}
