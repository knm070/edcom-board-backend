using Edcom.Api.Infrastructure.Data;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Authorization.Extensions;
using Edcom.Api.Modules.Authorization.Services;
using Edcom.Api.Modules.Issues.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Edcom.Api.Modules.Issues.Controllers;

[ApiController]
[Route("api/v1/spaces/{spaceId:guid}/issues")]
[Authorize]
public class IssuesController(AppDbContext db, IPermissionService perms) : ControllerBase
{
    private Guid CurrentUserId => User.GetUserId();

    // ── GET /api/v1/spaces/{spaceId}/issues  ──────────────────────────────────
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
            .Include(i => i.Status)
            .Include(i => i.Assignees).ThenInclude(a => a.User)
            .Where(i => i.SpaceId == spaceId && i.DeletedAt == null);

        if (backlog)
            query = query.Where(i => i.SprintId == null);
        else if (sprintId.HasValue)
            query = query.Where(i => i.SprintId == sprintId);

        if (epicId.HasValue)   query = query.Where(i => i.EpicId == epicId);
        if (status != null)    query = query.Where(i => i.Status.Name == status);
        if (priority != null && Enum.TryParse<IssuePriority>(priority, out var p))
            query = query.Where(i => i.Priority == p);

        var issues = await query.OrderBy(i => i.CreatedAt).ToListAsync(ct);
        return issues.Select(i => ToListDto(i, space.IssueKeyPrefix)).ToList();
    }

    // ── POST /api/v1/spaces/{spaceId}/issues  ─────────────────────────────────
    // RBAC: Admin, OrgTaskManager, Employer of that org
    [HttpPost]
    public async Task<ActionResult<IssueDetailDto>> Create(
        Guid spaceId, [FromBody] CreateIssueRequest req, CancellationToken ct)
    {
        var space = await RequireSpaceMember(spaceId, ct);

        if (!perms.CanWriteTicket(User, space.OrgId))
            throw new UnauthorizedAccessException("Only Admins, OrgTaskManagers, and Employers can create tickets.");

        if (!Enum.TryParse<IssueType>(req.Type, out var type))
            throw new InvalidOperationException("Invalid issue type.");
        if (!Enum.TryParse<IssuePriority>(req.Priority, out var priority))
            throw new InvalidOperationException("Invalid priority.");

        // Increment key counter (EF-tracked update; SQLite is single-writer so this is safe)
        space.IssueCounter++;
        space.UpdatedAt = DateTime.UtcNow;

        var issue = new Issue
        {
            SpaceId    = spaceId,
            OrgId      = space.OrgId,
            KeyNumber  = space.IssueCounter,
            Title      = req.Title,
            Description = req.Description,
            Type       = type,
            Priority   = priority,
            StatusId   = req.StatusId,
            SprintId   = req.SprintId,
            EpicId     = req.EpicId,
            ReporterId = CurrentUserId,
            StoryPoints = req.StoryPoints,
            DueDate    = req.DueDate
        };
        db.Issues.Add(issue);

        if (req.AssigneeIds?.Count > 0)
        {
            foreach (var uid in req.AssigneeIds)
                db.Set<IssueAssignee>().Add(new IssueAssignee
                {
                    IssueId = issue.Id, UserId = uid, AssignedById = CurrentUserId
                });
        }

        await db.SaveChangesAsync(ct);

        var full = await LoadFullIssue(issue.Id, ct);
        return CreatedAtAction(nameof(GetById), new { spaceId, issueId = issue.Id },
            ToDetailDto(full, space.IssueKeyPrefix));
    }

    // ── GET /api/v1/spaces/{spaceId}/issues/{issueId}  ────────────────────────
    [HttpGet("{issueId:guid}")]
    public async Task<ActionResult<IssueDetailDto>> GetById(
        Guid spaceId, Guid issueId, CancellationToken ct)
    {
        var space = await RequireSpaceMember(spaceId, ct);
        var issue = await LoadFullIssue(issueId, ct);
        if (issue.SpaceId != spaceId || issue.DeletedAt != null)
            throw new KeyNotFoundException("Issue not found.");
        return ToDetailDto(issue, space.IssueKeyPrefix);
    }

    // ── PATCH /api/v1/spaces/{spaceId}/issues/{issueId}  ──────────────────────
    [HttpPatch("{issueId:guid}")]
    public async Task<ActionResult<IssueDetailDto>> Update(
        Guid spaceId, Guid issueId, [FromBody] UpdateIssueRequest req, CancellationToken ct)
    {
        var space = await RequireSpaceMember(spaceId, ct);
        var issue = await db.Issues.FindAsync([issueId], ct)
            ?? throw new KeyNotFoundException("Issue not found.");
        if (issue.SpaceId != spaceId) throw new KeyNotFoundException("Issue not found.");

        if (req.Title != null)       issue.Title = req.Title;
        if (req.Description != null) issue.Description = req.Description;
        if (req.StoryPoints.HasValue) issue.StoryPoints = req.StoryPoints;
        if (req.DueDate.HasValue)    issue.DueDate = req.DueDate;
        if (req.SprintId.HasValue)   issue.SprintId = req.SprintId;
        if (req.EpicId.HasValue)     issue.EpicId = req.EpicId;
        if (req.StatusId.HasValue)   issue.StatusId = req.StatusId.Value;
        if (req.Type != null && Enum.TryParse<IssueType>(req.Type, out var t)) issue.Type = t;
        if (req.Priority != null && Enum.TryParse<IssuePriority>(req.Priority, out var p)) issue.Priority = p;

        issue.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var full = await LoadFullIssue(issueId, ct);
        return ToDetailDto(full, space.IssueKeyPrefix);
    }

    // ── POST /api/v1/spaces/{spaceId}/issues/{issueId}/move  ──────────────────
    // RBAC:
    //   - OrgTaskManager / Admin → any transition (bypass workflow)
    //   - Employer              → only transitions allowed by WorkflowTransition table
    [HttpPost("{issueId:guid}/move")]
    public async Task<ActionResult<IssueListDto>> Move(
        Guid spaceId, Guid issueId, [FromBody] MoveIssueRequest req, CancellationToken ct)
    {
        var space = await RequireSpaceMember(spaceId, ct);

        if (!perms.CanChangeStatus(User, space.OrgId))
            throw new UnauthorizedAccessException("You do not have permission to change ticket status.");

        var issue = await db.Issues
            .Include(i => i.Status)
            .Include(i => i.Assignees).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(i => i.Id == issueId && i.SpaceId == spaceId && i.DeletedAt == null, ct)
            ?? throw new KeyNotFoundException("Issue not found.");

        // Enforce workflow transitions for Employers
        if (!perms.CanBypassWorkflow(User, space.OrgId))
        {
            var transitionAllowed = await db.WorkflowTransitions.AnyAsync(
                t => t.SpaceId == spaceId &&
                     t.FromStatusId == issue.StatusId &&
                     t.ToStatusId == req.StatusId,
                ct);

            // Also check system-level transitions (SpaceId = null) for External spaces
            if (!transitionAllowed)
                transitionAllowed = await db.WorkflowTransitions.AnyAsync(
                    t => t.SpaceId == null &&
                         t.FromStatusId == issue.StatusId &&
                         t.ToStatusId == req.StatusId,
                    ct);

            if (!transitionAllowed)
                throw new InvalidOperationException(
                    "This status transition is not allowed by the workflow rules. " +
                    "An OrgTaskManager can override this restriction.");
        }

        issue.StatusId  = req.StatusId;
        issue.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await db.Entry(issue).Reference(i => i.Status).LoadAsync(ct);
        return ToListDto(issue, space.IssueKeyPrefix);
    }

    // ── PATCH /api/v1/spaces/{spaceId}/issues/{issueId}/assignees  ────────────
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

    // ── DELETE /api/v1/spaces/{spaceId}/issues/{issueId}  ────────────────────
    [HttpDelete("{issueId:guid}")]
    public async Task<IActionResult> Delete(Guid spaceId, Guid issueId, CancellationToken ct)
    {
        await RequireSpaceMember(spaceId, ct);
        var issue = await db.Issues.FindAsync([issueId], ct)
            ?? throw new KeyNotFoundException("Issue not found.");
        if (issue.SpaceId != spaceId) throw new KeyNotFoundException("Issue not found.");

        issue.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── POST /api/v1/spaces/{spaceId}/issues/{issueId}/comments  ─────────────
    [HttpPost("{issueId:guid}/comments")]
    public async Task<ActionResult<CommentDto>> AddComment(
        Guid spaceId, Guid issueId,
        [FromBody] AddCommentRequest req, CancellationToken ct)
    {
        var space = await RequireSpaceMember(spaceId, ct);
        var issue = await db.Issues.FindAsync([issueId], ct)
            ?? throw new KeyNotFoundException("Issue not found.");
        if (issue.SpaceId != spaceId) throw new KeyNotFoundException("Issue not found.");

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

    // ── Helpers  ──────────────────────────────────────────────────────────────
    private async Task<Space> RequireSpaceMember(Guid spaceId, CancellationToken ct)
    {
        var space = await db.Spaces.FindAsync([spaceId], ct)
            ?? throw new KeyNotFoundException("Space not found.");
        if (!await db.OrgMembers.AnyAsync(m => m.OrgId == space.OrgId && m.UserId == CurrentUserId, ct))
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
