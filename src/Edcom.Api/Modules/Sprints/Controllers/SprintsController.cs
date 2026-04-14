using Edcom.Api.Infrastructure.Data;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Authorization.Extensions;
using Edcom.Api.Modules.Sprints.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Edcom.Api.Modules.Sprints.Controllers;

[ApiController]
[Route("api/spaces/{spaceId:guid}/sprints")]
[Authorize]
public class SprintsController(AppDbContext db) : ControllerBase
{
    private Guid CurrentUserId => User.GetUserId();

    // ── GET /api/v1/spaces/{spaceId}/sprints ─────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<List<SprintDto>>> GetSprints(
        Guid spaceId, CancellationToken ct)
    {
        var space = await RequireSpaceMember(spaceId, ct);

        var sprints = await db.Sprints
            .AsNoTracking()
            .Where(s => s.SpaceId == spaceId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);

        if (sprints.Count == 0)
            return Ok(new List<SprintDto>());

        // Single aggregation query for issue stats across all sprints
        var sprintIds = sprints.Select(s => s.Id).ToList();
        var issueStats = await db.Issues
            .AsNoTracking()
            .Include(i => i.Status)
            .Where(i => i.SpaceId == spaceId
                     && i.SprintId != null
                     && sprintIds.Contains(i.SprintId.Value)
                     && i.DeletedAt == null)
            .ToListAsync(ct);

        var statsBySprint = issueStats
            .GroupBy(i => i.SprintId!.Value)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    IssueCount = g.Count(),
                    StoryPoints = g.Sum(i => i.StoryPoints ?? 0),
                    CompletedSP = g.Where(i => i.Status.IsDoneStatus).Sum(i => i.StoryPoints ?? 0),
                });

        return sprints.Select(s =>
        {
            var stats = statsBySprint.GetValueOrDefault(s.Id);
            return new SprintDto(
                s.Id, s.SpaceId, s.Name, s.Goal, s.StartDate, s.EndDate,
                s.Status.ToString(),
                stats?.IssueCount ?? 0,
                stats?.StoryPoints ?? 0,
                stats?.CompletedSP ?? 0,
                s.CreatedAt);
        }).ToList();
    }

    // ── GET /api/v1/spaces/{spaceId}/sprints/active ──────────────────────────
    [HttpGet("active")]
    public async Task<ActionResult<SprintDto>> GetActive(
        Guid spaceId, CancellationToken ct)
    {
        await RequireSpaceMember(spaceId, ct);

        var sprint = await db.Sprints
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SpaceId == spaceId && s.Status == SprintStatus.Active, ct);

        if (sprint is null) return NotFound();

        var issueStats = await db.Issues
            .AsNoTracking()
            .Include(i => i.Status)
            .Where(i => i.SpaceId == spaceId && i.SprintId == sprint.Id && i.DeletedAt == null)
            .ToListAsync(ct);

        return ToDto(sprint,
            issueStats.Count,
            issueStats.Sum(i => i.StoryPoints ?? 0),
            issueStats.Where(i => i.Status.IsDoneStatus).Sum(i => i.StoryPoints ?? 0));
    }

    // ── GET /api/v1/spaces/{spaceId}/sprints/velocity ────────────────────────
    // NOTE: Literal-segment routes must be declared BEFORE {sprintId:guid} routes.
    [HttpGet("velocity")]
    public async Task<ActionResult<List<SprintVelocityDto>>> GetVelocity(
        Guid spaceId, CancellationToken ct)
    {
        await RequireSpaceMember(spaceId, ct);

        var records = await db.Set<SprintVelocityRecord>()
            .AsNoTracking()
            .Include(r => r.Sprint)
            .Where(r => r.SpaceId == spaceId)
            .OrderBy(r => r.CompletedAt)
            .ToListAsync(ct);

        return records.Select(r => new SprintVelocityDto(
            r.SprintId, r.Sprint.Name,
            r.CommittedPoints, r.CompletedPoints,
            r.CompletedAt)).ToList();
    }

    // ── POST /api/v1/spaces/{spaceId}/sprints ────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<SprintDto>> Create(
        Guid spaceId, [FromBody] CreateSprintRequest req, CancellationToken ct)
    {
        var space = await RequireSpaceMember(spaceId, ct);
        RequireManager(space);

        var sprint = new Sprint
        {
            SpaceId   = spaceId,
            Name      = req.Name,
            Goal      = req.Goal,
            StartDate = req.StartDate,
            EndDate   = req.EndDate,
            Status    = SprintStatus.Planned,
        };
        db.Sprints.Add(sprint);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetSprints), new { spaceId },
            ToDto(sprint, 0, 0, 0));
    }

    // ── PATCH /api/v1/spaces/{spaceId}/sprints/{sprintId} ────────────────────
    [HttpPatch("{sprintId:guid}")]
    public async Task<ActionResult<SprintDto>> Update(
        Guid spaceId, Guid sprintId,
        [FromBody] UpdateSprintRequest req, CancellationToken ct)
    {
        var space = await RequireSpaceMember(spaceId, ct);
        RequireManager(space);

        var sprint = await db.Sprints.FindAsync([sprintId], ct)
            ?? throw new KeyNotFoundException("Sprint not found.");
        if (sprint.SpaceId != spaceId) throw new KeyNotFoundException("Sprint not found.");

        if (req.Name      != null) sprint.Name      = req.Name;
        if (req.Goal      != null) sprint.Goal      = req.Goal;
        if (req.StartDate != null) sprint.StartDate = req.StartDate;
        if (req.EndDate   != null) sprint.EndDate   = req.EndDate;
        sprint.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return ToDto(sprint, 0, 0, 0); // stats not critical on update response
    }

    // ── DELETE /api/v1/spaces/{spaceId}/sprints/{sprintId} ───────────────────
    [HttpDelete("{sprintId:guid}")]
    public async Task<IActionResult> Delete(
        Guid spaceId, Guid sprintId, CancellationToken ct)
    {
        var space = await RequireSpaceMember(spaceId, ct);
        RequireManager(space);

        var sprint = await db.Sprints.FindAsync([sprintId], ct)
            ?? throw new KeyNotFoundException("Sprint not found.");
        if (sprint.SpaceId != spaceId) throw new KeyNotFoundException("Sprint not found.");
        if (sprint.Status != SprintStatus.Planned)
            return Conflict("Only Planned sprints can be deleted.");

        // Move any issues back to backlog
        await db.Issues
            .Where(i => i.SprintId == sprintId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.SprintId, (Guid?)null), ct);

        db.Sprints.Remove(sprint);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── POST /api/v1/spaces/{spaceId}/sprints/{sprintId}/start ───────────────
    [HttpPost("{sprintId:guid}/start")]
    public async Task<ActionResult<SprintDto>> Start(
        Guid spaceId, Guid sprintId, CancellationToken ct)
    {
        var space = await RequireSpaceMember(spaceId, ct);
        RequireManager(space);

        var sprint = await db.Sprints.FindAsync([sprintId], ct)
            ?? throw new KeyNotFoundException("Sprint not found.");
        if (sprint.SpaceId != spaceId) throw new KeyNotFoundException("Sprint not found.");
        if (sprint.Status != SprintStatus.Planned)
            return Conflict("Only Planned sprints can be started.");

        var hasActive = await db.Sprints.AnyAsync(
            s => s.SpaceId == spaceId && s.Status == SprintStatus.Active, ct);
        if (hasActive)
            return Conflict("Another sprint is already active. Complete it first.");

        sprint.Status    = SprintStatus.Active;
        sprint.StartDate ??= DateTime.UtcNow;
        sprint.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return ToDto(sprint, 0, 0, 0);
    }

    // ── POST /api/v1/spaces/{spaceId}/sprints/{sprintId}/complete ────────────
    [HttpPost("{sprintId:guid}/complete")]
    public async Task<ActionResult<SprintDto>> Complete(
        Guid spaceId, Guid sprintId,
        [FromBody] CompleteSprintRequest req, CancellationToken ct)
    {
        var space = await RequireSpaceMember(spaceId, ct);
        RequireManager(space);

        var sprint = await db.Sprints
            .Include(s => s.Issues).ThenInclude(i => i.Status)
            .FirstOrDefaultAsync(s => s.Id == sprintId && s.SpaceId == spaceId, ct)
            ?? throw new KeyNotFoundException("Sprint not found.");

        if (sprint.Status != SprintStatus.Active)
            return Conflict("Only Active sprints can be completed.");

        var allIssues = sprint.Issues.Where(i => i.DeletedAt == null).ToList();

        // Separate done vs. incomplete issues
        var incompleteIssues = allIssues.Where(i => !i.Status.IsDoneStatus).ToList();

        if (req.Disposition == "next_sprint" && req.TargetSprintId.HasValue)
        {
            var targetSprint = await db.Sprints.FindAsync([req.TargetSprintId.Value], ct);
            if (targetSprint == null || targetSprint.SpaceId != spaceId)
                return BadRequest("Target sprint not found in this space.");
            foreach (var issue in incompleteIssues)
                issue.SprintId = req.TargetSprintId.Value;
        }
        else
        {
            // Move incomplete issues to backlog
            foreach (var issue in incompleteIssues)
                issue.SprintId = null;
        }

        // Record velocity snapshot
        var committedSP  = allIssues.Sum(i => i.StoryPoints ?? 0);
        var completedSP  = allIssues.Where(i => i.Status.IsDoneStatus).Sum(i => i.StoryPoints ?? 0);

        db.Set<SprintVelocityRecord>().Add(new SprintVelocityRecord
        {
            SprintId         = sprintId,
            SpaceId          = spaceId,
            CommittedPoints  = committedSP,
            CompletedPoints  = completedSP,
            CompletedAt      = DateTime.UtcNow,
        });

        sprint.Status    = SprintStatus.Completed;
        sprint.EndDate  ??= DateTime.UtcNow;
        sprint.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return ToDto(sprint, allIssues.Count, committedSP, completedSP);
    }

    // ── POST /api/v1/spaces/{spaceId}/sprints/{sprintId}/issues ─────────────
    /// <summary>Moves a single backlog issue into a sprint.</summary>
    [HttpPost("{sprintId:guid}/issues")]
    public async Task<IActionResult> AddIssue(
        Guid spaceId, Guid sprintId,
        [FromBody] SprintIssueRequest req, CancellationToken ct)
    {
        await RequireSpaceMember(spaceId, ct);

        var sprint = await db.Sprints.FindAsync([sprintId], ct)
            ?? throw new KeyNotFoundException("Sprint not found.");
        if (sprint.SpaceId != spaceId) return NotFound();

        var issue = await db.Issues.FindAsync([req.IssueId], ct)
            ?? throw new KeyNotFoundException("Issue not found.");
        if (issue.SpaceId != spaceId) return NotFound();

        issue.SprintId   = sprintId;
        issue.UpdatedAt  = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── DELETE /api/v1/spaces/{spaceId}/sprints/{sprintId}/issues/{issueId} ──
    /// <summary>Removes a single issue from a sprint (returns it to the backlog).</summary>
    [HttpDelete("{sprintId:guid}/issues/{issueId:guid}")]
    public async Task<IActionResult> RemoveIssue(
        Guid spaceId, Guid sprintId, Guid issueId, CancellationToken ct)
    {
        await RequireSpaceMember(spaceId, ct);

        var issue = await db.Issues.FindAsync([issueId], ct)
            ?? throw new KeyNotFoundException("Issue not found.");
        if (issue.SpaceId != spaceId || issue.SprintId != sprintId) return NotFound();

        issue.SprintId  = null;
        issue.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Space> RequireSpaceMember(Guid spaceId, CancellationToken ct)
    {
        var space = await db.Spaces.FindAsync([spaceId], ct)
            ?? throw new KeyNotFoundException("Space not found.");
        if (!User.IsMemberOfOrg(space.OrgId))
            throw new UnauthorizedAccessException("You are not a member of this organization.");
        return space;
    }

    private void RequireManager(Space space)
    {
        if (!User.HasOrgRole(space.OrgId, OrgRole.OrgManager))
            throw new UnauthorizedAccessException("Only OrgManagers can perform this action.");
    }

    private static SprintDto ToDto(Sprint s, int issueCount, int sp, int completedSP) =>
        new(s.Id, s.SpaceId, s.Name, s.Goal, s.StartDate, s.EndDate,
            s.Status.ToString(), issueCount, sp, completedSP, s.CreatedAt);
}
