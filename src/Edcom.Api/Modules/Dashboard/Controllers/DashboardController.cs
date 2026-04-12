using Edcom.Api.Infrastructure.Data;
using Edcom.Api.Modules.Authorization.Extensions;
using Edcom.Api.Modules.Authorization.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Edcom.Api.Modules.Dashboard.Controllers;

/// <summary>
/// GET /api/v1/dashboard
///
/// RBAC:
///   SystemAdmin   → all-org statistics
///   OrgTaskManager → own org stats + cross-org in-progress
///   Employer       → my tasks + deadlines
///   Viewer         → read-only board summaries
/// </summary>
[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController(AppDbContext db, IPermissionService perms) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var view = perms.GetDashboardView(User);

        return view switch
        {
            DashboardView.SystemWide  => Ok(await BuildSystemWideDashboard(ct)),
            DashboardView.OrgManager  => Ok(await BuildOrgManagerDashboard(ct)),
            DashboardView.MyTasks     => Ok(await BuildMyTasksDashboard(ct)),
            DashboardView.ReadOnly    => Ok(await BuildReadOnlyDashboard(ct)),
            _                         => Forbid()
        };
    }

    // ── System Admin: all-org statistics ─────────────────────────────────────
    private async Task<object> BuildSystemWideDashboard(CancellationToken ct)
    {
        var totalOrgs   = await db.Organizations.CountAsync(o => o.IsActive, ct);
        var totalUsers  = await db.Users.CountAsync(u => u.IsActive, ct);
        var totalIssues = await db.Issues.CountAsync(i => i.DeletedAt == null, ct);
        var openIssues  = await db.Issues
            .CountAsync(i => i.DeletedAt == null && !i.Status.IsTerminal, ct);

        return new
        {
            view    = "system_wide",
            stats   = new { totalOrgs, totalUsers, totalIssues, openIssues }
        };
    }

    // ── OrgTaskManager: own org(s) + cross-org in-progress ───────────────────
    private async Task<object> BuildOrgManagerDashboard(CancellationToken ct)
    {
        var userId   = User.GetUserId();
        var myOrgIds = User.GetOrgRoles().Select(r => r.OrgId).ToList();

        var orgStats = await db.Organizations
            .Where(o => myOrgIds.Contains(o.Id) && o.IsActive)
            .Select(o => new
            {
                o.Id,
                o.Name,
                memberCount = o.Members.Count,
                openIssues  = o.Spaces
                    .SelectMany(s => s.Issues)
                    .Count(i => i.DeletedAt == null && !i.Status.IsTerminal)
            })
            .ToListAsync(ct);

        var crossOrgInProgress = await db.CrossOrgTickets
            .Include(t => t.ExternalIssue).ThenInclude(i => i.Status)
            .Where(t => myOrgIds.Contains(t.CreatorOrgId) &&
                        !t.ExternalIssue.Status.IsTerminal &&
                        t.ExternalIssue.DeletedAt == null)
            .CountAsync(ct);

        return new
        {
            view = "org_manager",
            orgStats,
            crossOrgInProgress
        };
    }

    // ── Employer: my tasks + deadlines ────────────────────────────────────────
    private async Task<object> BuildMyTasksDashboard(CancellationToken ct)
    {
        var userId = User.GetUserId();

        var myIssues = await db.IssueAssignees
            .Include(a => a.Issue).ThenInclude(i => i.Status)
            .Include(a => a.Issue).ThenInclude(i => i.Space)
            .Where(a => a.UserId == userId &&
                        a.Issue.DeletedAt == null &&
                        !a.Issue.Status.IsTerminal)
            .OrderBy(a => a.Issue.DueDate)
            .Select(a => new
            {
                id       = a.Issue.Id,
                key      = a.Issue.Space.IssueKeyPrefix + "-" + a.Issue.KeyNumber,
                title    = a.Issue.Title,
                status   = a.Issue.Status.Name,
                priority = a.Issue.Priority.ToString(),
                dueDate  = a.Issue.DueDate
            })
            .ToListAsync(ct);

        return new { view = "my_tasks", issues = myIssues };
    }

    // ── Viewer: read-only board summaries ─────────────────────────────────────
    private async Task<object> BuildReadOnlyDashboard(CancellationToken ct)
    {
        var myOrgIds = User.GetOrgRoles().Select(r => r.OrgId).ToList();

        var summaries = await db.Spaces
            .Where(s => myOrgIds.Contains(s.OrgId))
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Type,
                issueCount = s.Issues.Count(i => i.DeletedAt == null)
            })
            .ToListAsync(ct);

        return new { view = "read_only", spaces = summaries };
    }
}
