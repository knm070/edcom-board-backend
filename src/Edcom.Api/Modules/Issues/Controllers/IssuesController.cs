using Edcom.Api.Infrastructure.Data;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Infrastructure.Results;
using Edcom.Api.Modules.Authorization.Extensions;
using Edcom.Api.Modules.Authorization.Services;
using Edcom.Api.Modules.Issues.Dto;
using Edcom.Api.Modules.Issues.Services;
using Edcom.Api.Modules.Spaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Edcom.Api.Modules.Issues.Controllers;

[ApiController]
[Route("api/spaces/{spaceId:guid}/issues")]
[Authorize]
public class IssuesController(AppDbContext db, IPermissionService perms, IWorkflowTransitionService transitions, ITicketService ticketService) : ControllerBase
{
    private Guid CurrentUserId => User.GetUserId();

    // ── GET /api/v1/spaces/{spaceId}/issues ──────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<List<IssueListDto>>> GetIssues(
        Guid spaceId,
        [FromQuery] Guid? sprintId,
        [FromQuery] Guid? epicId,
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] bool backlog = false,
        CancellationToken ct = default)
    {
        var space = await RequireSpaceMember(spaceId, ct);

        var query = db.Issues
            .AsNoTracking()
            .Include(i => i.Status)
            .Include(i => i.Assignees).ThenInclude(a => a.User)
            .Where(i => i.SpaceId == spaceId && i.DeletedAt == null);

        if (backlog)
            query = query.Where(i => i.SprintId == null);
        else if (sprintId.HasValue)
            query = query.Where(i => i.SprintId == sprintId);

        if (epicId.HasValue)
            query = query.Where(i => i.EpicId == epicId);
        if (status != null)
            query = query.Where(i => i.Status.Name == status);
        if (priority != null && Enum.TryParse<IssuePriority>(priority, out var p))
            query = query.Where(i => i.Priority == p);

        // Backlog respects manual ordering; sprint/board fallback to creation order
        var issues = backlog
            ? await query.OrderBy(i => i.BacklogOrder).ThenBy(i => i.CreatedAt).ToListAsync(ct)
            : await query.OrderBy(i => i.BacklogOrder).ThenBy(i => i.CreatedAt).ToListAsync(ct);

        return issues.Select(i => ToListDto(i, space.IssueKeyPrefix)).ToList();
    }

    // ── POST /api/v1/spaces/{spaceId}/issues ─────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create(
        Guid spaceId, [FromBody] CreateIssueRequest req, CancellationToken ct)
    {
        await RequireSpaceMember(spaceId, ct);
        var result = await ticketService.CreateAsync(spaceId, req, User, ct);

        return result.Match(
            onSuccess: dto => (IActionResult)CreatedAtAction(nameof(GetById), new { spaceId, issueId = dto.Id }, dto),
            onFailure: failure => (IActionResult)BadRequest(new { code = failure.Code, message = failure.Message }),
            onUnauthorized: _ => (IActionResult)Forbid(),
            onNotFound: notFound => (IActionResult)NotFound(new { entity = notFound.Entity }),
            onConflict: conflict => (IActionResult)Conflict(new { message = conflict.Message }));
    }

    // ── GET /api/v1/spaces/{spaceId}/issues/{issueId} ────────────────────────
    [HttpGet("{issueId:guid}")]
    public async Task<ActionResult<IssueDetailDto>> GetById(
        Guid spaceId, Guid issueId, CancellationToken ct)
    {
        var space = await RequireSpaceMember(spaceId, ct);
        var issue = await LoadFullIssue(issueId, ct);
        if (issue.SpaceId != spaceId || issue.DeletedAt != null)
            return NotFound();
        return ToDetailDto(issue, space.IssueKeyPrefix);
    }

    // ── PATCH /api/v1/spaces/{spaceId}/issues/{issueId} ──────────────────────
    [HttpPatch("{issueId:guid}")]
    public async Task<IActionResult> Update(
        Guid spaceId, Guid issueId, [FromBody] UpdateIssueRequest req, CancellationToken ct)
    {
        await RequireSpaceMember(spaceId, ct);
        var result = await ticketService.UpdateAsync(spaceId, issueId, req, User, ct);

        return result.Match(
            onSuccess: dto => (IActionResult)Ok(dto),
            onFailure: failure => (IActionResult)BadRequest(new { code = failure.Code, message = failure.Message }),
            onUnauthorized: _ => (IActionResult)Forbid(),
            onNotFound: notFound => (IActionResult)NotFound(new { entity = notFound.Entity }),
            onConflict: conflict => (IActionResult)Conflict(new { message = conflict.Message }));
    }

    // ── POST /api/v1/spaces/{spaceId}/issues/reorder ─────────────────────────
    /// <summary>
    /// Persists the manual backlog ordering. Accepts an ordered list of issue IDs
    /// and sets BacklogOrder = index for each. Only issues belonging to this space
    /// are updated; unknown IDs are silently ignored.
    /// </summary>
    [HttpPost("reorder")]
    public async Task<IActionResult> Reorder(
        Guid spaceId, [FromBody] ReorderIssuesRequest req, CancellationToken ct)
    {
        await RequireSpaceMember(spaceId, ct);
        if (req.OrderedIds.Count == 0) return NoContent();

        var issues = await db.Issues
            .Where(i => i.SpaceId == spaceId && req.OrderedIds.Contains(i.Id) && i.DeletedAt == null)
            .ToDictionaryAsync(i => i.Id, ct);

        for (int idx = 0; idx < req.OrderedIds.Count; idx++)
        {
            if (issues.TryGetValue(req.OrderedIds[idx], out var issue))
                issue.BacklogOrder = idx;
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── PATCH /api/v1/spaces/{spaceId}/issues/bulk ───────────────────────────
    /// <summary>
    /// Applies a single field update to multiple issues at once.
    /// Set <c>ClearSprint = true</c> to move issues to the backlog (SprintId → null).
    /// Set <c>ClearEpic = true</c> to detach issues from an epic (EpicId → null).
    /// Set <c>Delete = true</c> to soft-delete all specified issues.
    /// </summary>
    [HttpPatch("bulk")]
    public async Task<IActionResult> BulkUpdate(
        Guid spaceId, [FromBody] BulkUpdateIssueRequest req, CancellationToken ct)
    {
        await RequireSpaceMember(spaceId, ct);
        if (req.IssueIds.Count == 0) return NoContent();

        var issues = await db.Issues
            .Where(i => i.SpaceId == spaceId && req.IssueIds.Contains(i.Id) && i.DeletedAt == null)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var issue in issues)
        {
            if (req.Delete)
            {
                issue.DeletedAt = now;
                issue.UpdatedAt = now;
                continue;
            }

            if (req.ClearSprint)         issue.SprintId = null;
            else if (req.SprintId != null) issue.SprintId = req.SprintId;

            if (req.ClearEpic)          issue.EpicId = null;
            else if (req.EpicId != null)  issue.EpicId = req.EpicId;

            if (req.Priority != null && Enum.TryParse<IssuePriority>(req.Priority, out var p))
                issue.Priority = p;

            issue.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── POST /api/v1/spaces/{spaceId}/issues/{issueId}/move ──────────────────
    [HttpPost("{issueId:guid}/move")]
    public async Task<ActionResult<IssueListDto>> Move(
        Guid spaceId, Guid issueId, [FromBody] MoveIssueRequest req, CancellationToken ct)
    {
        var space = await RequireSpaceMember(spaceId, ct);

        if (!perms.CanWriteTicket(User, space.OrgId))
            return Forbid();

        var issue = await db.Issues
            .Include(i => i.Status)
            .Include(i => i.Assignees).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(i => i.Id == issueId && i.SpaceId == spaceId && i.DeletedAt == null, ct)
            ?? throw new KeyNotFoundException("Issue not found.");

        var callerRole = User.GetOrgRole(space.OrgId)
            ?? throw new UnauthorizedAccessException("You are not a member of this organization.");
        await transitions.ValidateAsync(space, issue.StatusId, req.StatusId, callerRole, ct);

        issue.StatusId  = req.StatusId;
        issue.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await db.Entry(issue).Reference(i => i.Status).LoadAsync(ct);
        return ToListDto(issue, space.IssueKeyPrefix);
    }

    // ── PATCH /api/v1/spaces/{spaceId}/issues/{issueId}/assignees ────────────
    [HttpPatch("{issueId:guid}/assignees")]
    public async Task<IActionResult> UpdateAssignees(
        Guid spaceId, Guid issueId, [FromBody] AssignIssueRequest req, CancellationToken ct)
    {
        await RequireSpaceMember(spaceId, ct);
        var issue = await db.Issues
            .Include(i => i.Assignees)
            .FirstOrDefaultAsync(i => i.Id == issueId && i.SpaceId == spaceId, ct)
            ?? throw new KeyNotFoundException("Issue not found.");

        db.Set<IssueAssignee>().RemoveRange(issue.Assignees);
        foreach (var uid in req.AssigneeIds)
            db.Set<IssueAssignee>().Add(new IssueAssignee
            {
                IssueId = issueId, UserId = uid, AssignedById = CurrentUserId
            });

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── DELETE /api/v1/spaces/{spaceId}/issues/{issueId} ─────────────────────
    [HttpDelete("{issueId:guid}")]
    public async Task<IActionResult> Delete(Guid spaceId, Guid issueId, CancellationToken ct)
    {
        await RequireSpaceMember(spaceId, ct);
        var issue = await db.Issues.FindAsync([issueId], ct)
            ?? throw new KeyNotFoundException("Issue not found.");
        if (issue.SpaceId != spaceId) return NotFound();

        issue.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── POST /api/v1/spaces/{spaceId}/issues/{issueId}/comments ──────────────
    [HttpPost("{issueId:guid}/comments")]
    public async Task<ActionResult<CommentDto>> AddComment(
        Guid spaceId, Guid issueId,
        [FromBody] AddCommentRequest req, CancellationToken ct)
    {
        var space = await RequireSpaceMember(spaceId, ct);
        var issue = await db.Issues.FindAsync([issueId], ct)
            ?? throw new KeyNotFoundException("Issue not found.");
        if (issue.SpaceId != spaceId) return NotFound();

        var comment = new Comment
        {
            IssueId     = issueId,
            AuthorId    = CurrentUserId,
            AuthorOrgId = space.OrgId,
            Body        = req.Body,
            ParentId    = req.ParentId
        };
        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);

        await db.Entry(comment).Reference(c => c.Author).LoadAsync(ct);
        return new CommentDto(comment.Id, comment.AuthorId, comment.Author.FullName,
            comment.Author.AvatarUrl, comment.Body, comment.ParentId, comment.CreatedAt);
    }

    // ── POST /api/v1/spaces/{spaceId}/issues/{issueId}/worklogs ───────────────
    [HttpPost("{issueId:guid}/worklogs")]
    public async Task<IActionResult> LogTime(
        Guid spaceId, Guid issueId,
        [FromBody] LogTimeRequest req, CancellationToken ct)
    {
        await RequireSpaceMember(spaceId, ct);
        var result = await ticketService.LogTimeAsync(spaceId, issueId, req, User, ct);

        return result.Match(
            onSuccess: _ => (IActionResult)NoContent(),
            onFailure: failure => (IActionResult)BadRequest(new { code = failure.Code, message = failure.Message }),
            onUnauthorized: _ => (IActionResult)Forbid(),
            onNotFound: notFound => (IActionResult)NotFound(new { entity = notFound.Entity }),
            onConflict: conflict => (IActionResult)Conflict(new { message = conflict.Message }));
    }

    // ── GET /api/v1/spaces/{spaceId}/issues/{issueId}/worklogs ────────────────
    [HttpGet("{issueId:guid}/worklogs")]
    public async Task<ActionResult<List<WorklogDto>>> GetWorklogs(
        Guid spaceId, Guid issueId, CancellationToken ct)
    {
        await RequireSpaceMember(spaceId, ct);

        var issue = await db.Issues
            .Include(i => i.Space)
            .FirstOrDefaultAsync(i => i.Id == issueId && i.SpaceId == spaceId && i.DeletedAt == null, ct);

        if (issue is null)
            return NotFound();

        var worklogs = await db.Set<Worklog>()
            .Where(w => w.IssueId == issueId)
            .Include(w => w.User)
            .OrderByDescending(w => w.Date)
            .ThenByDescending(w => w.CreatedAt)
            .ToListAsync(ct);

        return worklogs.Select(w => new WorklogDto(
            w.Id, w.IssueId, w.UserId, w.User.FullName, w.User.AvatarUrl,
            w.Hours, w.Description, w.Date, w.CreatedAt)).ToList();
    }

    // ── GET /api/v1/spaces/{spaceId}/issues/{issueId}/activity ────────────────
    [HttpGet("{issueId:guid}/activity")]
    public async Task<ActionResult<List<ActivityLogDto>>> GetActivityLog(
        Guid spaceId, Guid issueId, CancellationToken ct)
    {
        await RequireSpaceMember(spaceId, ct);

        var issue = await db.Issues
            .FirstOrDefaultAsync(i => i.Id == issueId && i.SpaceId == spaceId && i.DeletedAt == null, ct);

        if (issue is null)
            return NotFound();

        var activityLogs = await db.Set<ActivityLog>()
            .Where(a => a.IssueId == issueId)
            .Include(a => a.Actor)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return activityLogs.Select(a => new ActivityLogDto(
            a.Id, a.IssueId, a.ActorId, a.Actor.FullName, a.Actor.AvatarUrl,
            a.Action, a.Metadata, a.CreatedAt)).ToList();
    }

    // ── PATCH /api/v1/spaces/{spaceId}/issues/{issueId}/worklogs/{worklogId} ──
    [HttpPatch("{issueId:guid}/worklogs/{worklogId:guid}")]
    public async Task<ActionResult<WorklogDto>> UpdateWorklog(
        Guid spaceId, Guid issueId, Guid worklogId,
        [FromBody] UpdateWorklogRequest req, CancellationToken ct)
    {
        var userId = CurrentUserId;
        await RequireSpaceMember(spaceId, ct);

        var worklog = await db.Set<Worklog>()
            .Include(w => w.User)
            .FirstOrDefaultAsync(w => w.Id == worklogId && w.IssueId == issueId, ct);

        if (worklog is null)
            return NotFound();

        // Only the author or managers can update
        var space = await db.Spaces.FindAsync([spaceId], ct)
            ?? throw new KeyNotFoundException("Space not found.");
        var userRole = User.GetOrgRole(space.OrgId);
        if (worklog.UserId != userId && (userRole is null || userRole != OrgRole.OrgManager))
            return Forbid();

        if (req.Hours.HasValue)
            worklog.Hours = req.Hours.Value;
        if (req.Description != null)
            worklog.Description = req.Description;
        if (req.Date.HasValue)
            worklog.Date = req.Date.Value;

        await db.SaveChangesAsync(ct);

        return new WorklogDto(
            worklog.Id, worklog.IssueId, worklog.UserId, worklog.User.FullName, worklog.User.AvatarUrl,
            worklog.Hours, worklog.Description, worklog.Date, worklog.CreatedAt);
    }

    // ── DELETE /api/v1/spaces/{spaceId}/issues/{issueId}/worklogs/{worklogId} ─
    [HttpDelete("{issueId:guid}/worklogs/{worklogId:guid}")]
    public async Task<IActionResult> DeleteWorklog(
        Guid spaceId, Guid issueId, Guid worklogId, CancellationToken ct)
    {
        var userId = CurrentUserId;
        await RequireSpaceMember(spaceId, ct);

        var worklog = await db.Set<Worklog>()
            .FirstOrDefaultAsync(w => w.Id == worklogId && w.IssueId == issueId, ct);

        if (worklog is null)
            return NotFound();

        // Only the author or managers can delete
        var space = await db.Spaces.FindAsync([spaceId], ct)
            ?? throw new KeyNotFoundException("Space not found.");
        var userRole = User.GetOrgRole(space.OrgId);
        if (worklog.UserId != userId && (userRole is null || userRole != OrgRole.OrgManager))
            return Forbid();

        db.Set<Worklog>().Remove(worklog);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Space> RequireSpaceMember(Guid spaceId, CancellationToken ct)
    {
        var space = await db.Spaces.FindAsync([spaceId], ct)
            ?? throw new KeyNotFoundException("Space not found.");
        if (!User.IsSystemAdmin() &&
            !await db.OrgMembers.AnyAsync(m => m.OrgId == space.OrgId && m.UserId == CurrentUserId, ct))
            throw new UnauthorizedAccessException("You are not a member of this organization.");
        return space;
    }

    private async Task<Issue> LoadFullIssue(Guid issueId, CancellationToken ct) =>
        await db.Issues
            .Include(i => i.Status)
            .Include(i => i.Assignees).ThenInclude(a => a.User)
            .Include(i => i.Reporter)
            .Include(i => i.Comments.Where(c => c.DeletedAt == null))
                .ThenInclude(c => c.Author)
            .FirstOrDefaultAsync(i => i.Id == issueId, ct)
        ?? throw new KeyNotFoundException("Issue not found.");

    private static IssueListDto ToListDto(Issue i, string prefix) => new(
        i.Id, $"{prefix}-{i.KeyNumber}", i.Title, i.Type.ToString(), i.Priority.ToString(),
        i.Status.Name, i.Status.Color, i.StatusId,
        i.Assignees.Select(a => new AssigneeDto(a.UserId, a.User.FullName, a.User.AvatarUrl)).ToList(),
        i.SprintId, i.EpicId, i.StoryPoints, i.DueDate, i.CreatedAt);

    private static IssueDetailDto ToDetailDto(Issue i, string prefix) => new(
        i.Id, $"{prefix}-{i.KeyNumber}", i.Title, i.Description, i.Type.ToString(),
        i.Priority.ToString(), i.Status.Name, i.Status.Color, i.StatusId,
        i.Assignees.Select(a => new AssigneeDto(a.UserId, a.User.FullName, a.User.AvatarUrl)).ToList(),
        new AssigneeDto(i.ReporterId, i.Reporter.FullName, i.Reporter.AvatarUrl),
        i.SprintId, i.EpicId, i.StoryPoints, i.DueDate,
        i.Comments.OrderBy(c => c.CreatedAt)
            .Select(c => new CommentDto(c.Id, c.AuthorId, c.Author.FullName,
                c.Author.AvatarUrl, c.Body, c.ParentId, c.CreatedAt)).ToList(),
        i.CreatedAt, i.UpdatedAt);
}

public record AddCommentRequest(string Body, Guid? ParentId);
