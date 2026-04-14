using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Infrastructure.Results;
using Edcom.Api.Modules.Issues.Services;
using Edcom.Api.Tests.Authorization;

namespace Edcom.Api.Tests.Issues;

/// <summary>
/// Unit tests for TicketService.LogTimeAsync role-based restrictions.
/// </summary>
public sealed class TicketLogTimeTests : TicketServiceTestBase
{
    private static LogTimeRequest ValidLogReq() => new(
        Hours:       1.5m,
        Date:        DateTime.UtcNow,
        Description: "Worked on it"
    );

    [Fact]
    public async Task OrgManager_CanLogTime_OnAnyTicket()
    {
        var issue = SeedIssue(reporterId: EmployerUserId);
        var manager = new ClaimsPrincipalBuilder()
            .WithUserId(ManagerUserId)
            .WithOrgRole(OrgId, OrgRole.OrgManager)
            .Build();

        var result = await Svc.LogTimeAsync(SpaceId, issue.Id, ValidLogReq(), manager, default);

        Assert.True(result.IsSuccess);

        var worklog = Db.Set<Worklog>().FirstOrDefault(w => w.IssueId == issue.Id);
        Assert.NotNull(worklog);
        Assert.Equal(1.5m, worklog.Hours);
    }

    [Fact]
    public async Task Employer_CanLogTime_OnOwnTicket()
    {
        var issue = SeedIssue(reporterId: EmployerUserId);
        var employer = new ClaimsPrincipalBuilder()
            .WithUserId(EmployerUserId)
            .WithOrgRole(OrgId, OrgRole.Employer)
            .Build();

        var result = await Svc.LogTimeAsync(SpaceId, issue.Id, ValidLogReq(), employer, default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Employer_CannotLogTime_OnOthersTicket()
    {
        // Issue belongs to the manager
        var issue = SeedIssue(reporterId: ManagerUserId);
        var employer = new ClaimsPrincipalBuilder()
            .WithUserId(EmployerUserId)
            .WithOrgRole(OrgId, OrgRole.Employer)
            .Build();

        var result = await Svc.LogTimeAsync(SpaceId, issue.Id, ValidLogReq(), employer, default);

        Assert.False(result.IsSuccess);
        Assert.IsType<Result.Unauthorized>(result.Outcome);
    }

    [Fact]
    public async Task LogTime_SpaceNotFound_ReturnsNotFound()
    {
        var issue = SeedIssue(reporterId: ManagerUserId);
        var manager = new ClaimsPrincipalBuilder()
            .WithUserId(ManagerUserId)
            .WithOrgRole(OrgId, OrgRole.OrgManager)
            .Build();

        var result = await Svc.LogTimeAsync(Guid.NewGuid(), issue.Id, ValidLogReq(), manager, default);

        Assert.False(result.IsSuccess);
        Assert.IsType<Result.NotFound>(result.Outcome);
    }

    [Fact]
    public async Task LogTime_IssueNotFound_ReturnsNotFound()
    {
        var manager = new ClaimsPrincipalBuilder()
            .WithUserId(ManagerUserId)
            .WithOrgRole(OrgId, OrgRole.OrgManager)
            .Build();

        var result = await Svc.LogTimeAsync(SpaceId, Guid.NewGuid(), ValidLogReq(), manager, default);

        Assert.False(result.IsSuccess);
        Assert.IsType<Result.NotFound>(result.Outcome);
    }

    [Fact]
    public async Task LogTime_CreatesActivityLog()
    {
        var issue = SeedIssue(reporterId: ManagerUserId);
        var manager = new ClaimsPrincipalBuilder()
            .WithUserId(ManagerUserId)
            .WithOrgRole(OrgId, OrgRole.OrgManager)
            .Build();

        await Svc.LogTimeAsync(SpaceId, issue.Id, ValidLogReq(), manager, default);

        var activity = Db.Set<ActivityLog>().FirstOrDefault(a => a.IssueId == issue.Id);
        Assert.NotNull(activity);
        Assert.Equal("LoggedTime", activity.Action);
    }
}
