using Edcom.Api.Infrastructure.Data;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Authorization.Extensions;
using Edcom.Api.Modules.Epics.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Edcom.Api.Modules.Epics.Controllers;

[ApiController]
[Route("api/spaces/{spaceId:guid}/epics")]
[Authorize]
public class EpicsController(AppDbContext db) : ControllerBase
{
    private Guid CurrentUserId => User.GetUserId();

    // ── GET /api/v1/spaces/{spaceId}/epics ───────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<List<EpicDto>>> GetEpics(
        Guid spaceId, CancellationToken ct)
    {
        await RequireSpaceMember(spaceId, ct);

        var epics = await db.Epics
            .AsNoTracking()
            .Where(e => e.SpaceId == spaceId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);

        if (epics.Count == 0)
            return Ok(new List<EpicDto>());

        // Aggregate issue stats for all epics in one query
        var epicIds = epics.Select(e => e.Id).ToList();
        var issueStats = await db.Issues
            .AsNoTracking()
            .Include(i => i.Status)
            .Where(i => i.SpaceId == spaceId
                     && i.EpicId != null
                     && epicIds.Contains(i.EpicId.Value)
                     && i.DeletedAt == null)
            .ToListAsync(ct);

        var statsByEpic = issueStats
            .GroupBy(i => i.EpicId!.Value)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    IssueCount          = g.Count(),
                    CompletedIssueCount = g.Count(i => i.Status.IsDoneStatus),
                    StoryPoints         = g.Sum(i => i.StoryPoints ?? 0),
                    CompletedSP         = g.Where(i => i.Status.IsDoneStatus).Sum(i => i.StoryPoints ?? 0),
                });

        return epics.Select(e =>
        {
            var s = statsByEpic.GetValueOrDefault(e.Id);
            return new EpicDto(
                e.Id, e.SpaceId, e.OrgId,
                e.Title, e.Description, e.Color,
                e.StartDate, e.EndDate,
                s?.IssueCount ?? 0,
                s?.CompletedIssueCount ?? 0,
                s?.StoryPoints ?? 0,
                s?.CompletedSP ?? 0,
                e.CreatedAt);
        }).ToList();
    }

    // ── POST /api/v1/spaces/{spaceId}/epics ──────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<EpicDto>> Create(
        Guid spaceId, [FromBody] CreateEpicRequest req, CancellationToken ct)
    {
        var space = await RequireSpaceMember(spaceId, ct);
        RequireManager(space);

        var epic = new Epic
        {
            SpaceId     = spaceId,
            OrgId       = space.OrgId,
            Title       = req.Title,
            Description = req.Description,
            Color       = req.Color ?? "#6366F1",
            StartDate   = req.StartDate,
            EndDate     = req.EndDate,
            CreatedById = CurrentUserId,
        };
        db.Epics.Add(epic);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetEpics), new { spaceId },
            new EpicDto(epic.Id, epic.SpaceId, epic.OrgId, epic.Title, epic.Description,
                epic.Color, epic.StartDate, epic.EndDate, 0, 0, 0, 0, epic.CreatedAt));
    }

    // ── PATCH /api/v1/spaces/{spaceId}/epics/{epicId} ────────────────────────
    [HttpPatch("{epicId:guid}")]
    public async Task<ActionResult<EpicDto>> Update(
        Guid spaceId, Guid epicId,
        [FromBody] UpdateEpicRequest req, CancellationToken ct)
    {
        var space = await RequireSpaceMember(spaceId, ct);
        RequireManager(space);

        var epic = await db.Epics.FindAsync([epicId], ct)
            ?? throw new KeyNotFoundException("Epic not found.");
        if (epic.SpaceId != spaceId) throw new KeyNotFoundException("Epic not found.");

        if (req.Title       != null) epic.Title       = req.Title;
        if (req.Description != null) epic.Description = req.Description;
        if (req.Color       != null) epic.Color       = req.Color;
        if (req.StartDate   != null) epic.StartDate   = req.StartDate;
        if (req.EndDate     != null) epic.EndDate     = req.EndDate;
        epic.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return new EpicDto(epic.Id, epic.SpaceId, epic.OrgId, epic.Title, epic.Description,
            epic.Color, epic.StartDate, epic.EndDate, 0, 0, 0, 0, epic.CreatedAt);
    }

    // ── DELETE /api/v1/spaces/{spaceId}/epics/{epicId} ───────────────────────
    [HttpDelete("{epicId:guid}")]
    public async Task<IActionResult> Delete(
        Guid spaceId, Guid epicId, CancellationToken ct)
    {
        var space = await RequireSpaceMember(spaceId, ct);
        RequireManager(space);

        var epic = await db.Epics.FindAsync([epicId], ct)
            ?? throw new KeyNotFoundException("Epic not found.");
        if (epic.SpaceId != spaceId) throw new KeyNotFoundException("Epic not found.");

        // Detach issues from this epic (do not delete them)
        await db.Issues
            .Where(i => i.EpicId == epicId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.EpicId, (Guid?)null), ct);

        db.Epics.Remove(epic);
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
}
