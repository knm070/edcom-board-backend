using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Infrastructure.Results;
using Edcom.Api.Modules.Issues.Dto;
using Edcom.Api.Tests.Authorization;

namespace Edcom.Api.Tests.Issues;

/// <summary>
/// Unit tests for TicketService.UpdateAsync field-level permission rules.
/// </summary>
public sealed class TicketUpdateTests : TicketServiceTestBase
{
    // ── OrgManager ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task OrgManager_CanUpdateTitle_OnAnyTicket()
    {
        var issue = SeedIssue(reporterId: EmployerUserId);
        var manager = new ClaimsPrincipalBuilder()
            .WithUserId(ManagerUserId)
            .WithOrgRole(OrgId, OrgRole.OrgManager)
            .Build();

        var result = await Svc.UpdateAsync(SpaceId, issue.Id,
            new UpdateIssueRequest("New title", null, null, null, null, null, null, null, null),
            manager, default);

        Assert.True(result.IsSuccess);
        Assert.Equal("New title", result.Data!.Title);
    }

    [Fact]
    public async Task OrgManager_CanUpdateStatus_BypassingWorkflow()
    {
        // Seed an issue in Done status — no workflow transition back to To Do is configured
        var issue = SeedIssue(reporterId: EmployerUserId);
        var manager = new ClaimsPrincipalBuilder()
            .WithUserId(ManagerUserId)
            .WithOrgRole(OrgId, OrgRole.OrgManager)
            .Build();

        // Manager should be able to set any status freely
        var result = await Svc.UpdateAsync(SpaceId, issue.Id,
            new UpdateIssueRequest(null, null, null, null, StatusDoneId, null, null, null, null),
            manager, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusDoneId, result.Data!.StatusId);
    }

    // ── Employer ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Employer_CanUpdateOwnTicket_Title()
    {
        var issue = SeedIssue(reporterId: EmployerUserId);
        var employer = new ClaimsPrincipalBuilder()
            .WithUserId(EmployerUserId)
            .WithOrgRole(OrgId, OrgRole.Employer)
            .Build();

        var result = await Svc.UpdateAsync(SpaceId, issue.Id,
            new UpdateIssueRequest("Updated title", null, null, null, null, null, null, null, null),
            employer, default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated title", result.Data!.Title);
    }

    [Fact]
    public async Task Employer_CannotUpdateOthersTicket_Title()
    {
        // Issue owned by manager, employer tries to update it
        var issue = SeedIssue(reporterId: ManagerUserId);
        var employer = new ClaimsPrincipalBuilder()
            .WithUserId(EmployerUserId)
            .WithOrgRole(OrgId, OrgRole.Employer)
            .Build();

        var result = await Svc.UpdateAsync(SpaceId, issue.Id,
            new UpdateIssueRequest("Hijacked title", null, null, null, null, null, null, null, null),
            employer, default);

        Assert.False(result.IsSuccess);
        Assert.IsType<Result.Unauthorized>(result.Outcome);
    }

    [Fact]
    public async Task Employer_CanTransitionStatus_WithValidWorkflow()
    {
        var issue = SeedIssue(reporterId: EmployerUserId);
        var employer = new ClaimsPrincipalBuilder()
            .WithUserId(EmployerUserId)
            .WithOrgRole(OrgId, OrgRole.Employer)
            .Build();

        // To Do → Done is configured in seed data with no role restriction
        var result = await Svc.UpdateAsync(SpaceId, issue.Id,
            new UpdateIssueRequest(null, null, null, null, StatusDoneId, null, null, null, null),
            employer, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusDoneId, result.Data!.StatusId);
    }

    [Fact]
    public async Task Employer_CannotTransitionStatus_WithNoTransitionDefined()
    {
        // First move issue to Done, then try to go back to To Do (no return transition seeded)
        var issue = SeedIssue(reporterId: EmployerUserId);
        issue.StatusId = StatusDoneId;
        await Db.SaveChangesAsync();

        var employer = new ClaimsPrincipalBuilder()
            .WithUserId(EmployerUserId)
            .WithOrgRole(OrgId, OrgRole.Employer)
            .Build();

        var result = await Svc.UpdateAsync(SpaceId, issue.Id,
            new UpdateIssueRequest(null, null, null, null, StatusTodoId, null, null, null, null),
            employer, default);

        Assert.False(result.IsSuccess);
        Assert.IsType<Result.Conflict>(result.Outcome);
    }

    // ── Not found / authorization ──────────────────────────────────────────────

    [Fact]
    public async Task Update_WrongSpace_ReturnsNotFound()
    {
        var issue = SeedIssue(reporterId: ManagerUserId);
        var manager = new ClaimsPrincipalBuilder()
            .WithUserId(ManagerUserId)
            .WithOrgRole(OrgId, OrgRole.OrgManager)
            .Build();

        var result = await Svc.UpdateAsync(Guid.NewGuid(), issue.Id,
            new UpdateIssueRequest("x", null, null, null, null, null, null, null, null),
            manager, default);

        Assert.False(result.IsSuccess);
        Assert.IsType<Result.NotFound>(result.Outcome);
    }

    [Fact]
    public async Task Update_NonMember_ReturnsUnauthorized()
    {
        var issue = SeedIssue(reporterId: ManagerUserId);
        var stranger = new ClaimsPrincipalBuilder()
            .WithUserId(Guid.NewGuid())
            .WithOrgRole(Guid.NewGuid(), OrgRole.OrgManager)
            .Build();

        var result = await Svc.UpdateAsync(SpaceId, issue.Id,
            new UpdateIssueRequest("x", null, null, null, null, null, null, null, null),
            stranger, default);

        Assert.False(result.IsSuccess);
        Assert.IsType<Result.Unauthorized>(result.Outcome);
    }
}
