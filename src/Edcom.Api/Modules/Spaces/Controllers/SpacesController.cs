using System.Security.Claims;
using System.Text.Json;
using Edcom.Api.Infrastructure.Data;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Authorization.Extensions;
using Edcom.Api.Modules.Spaces.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Edcom.Api.Modules.Spaces.Controllers;

[ApiController]
[Route("api/orgs/{orgId:guid}/spaces")]
[Authorize]
public class SpacesController(AppDbContext db) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    private static readonly JsonSerializerOptions _json =
        new() { PropertyNameCaseInsensitive = true };

    // ── GET /api/v1/orgs/{orgId}/spaces ──────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<List<SpaceDto>>> GetSpaces(Guid orgId, CancellationToken ct)
    {
        await RequireMember(orgId, ct);
        var spaces = await db.Spaces
            .Include(s => s.WorkflowStatuses.OrderBy(w => w.Position))
            .Include(s => s.Issues.Where(i => i.DeletedAt == null))
            .Where(s => s.OrgId == orgId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);

        return spaces.Select(ToDto).ToList();
    }

    // ── GET /api/v1/orgs/{orgId}/spaces/{spaceId} ────────────────────────────

    [HttpGet("{spaceId:guid}")]
    public async Task<ActionResult<SpaceDto>> GetSpace(Guid orgId, Guid spaceId, CancellationToken ct)
    {
        await RequireMember(orgId, ct);
        return ToDto(await LoadSpaceAsync(orgId, spaceId, ct));
    }

    // ── POST /api/v1/orgs/{orgId}/spaces ─────────────────────────────────────

    [HttpPost]
    public async Task<ActionResult<SpaceDto>> CreateSpace(
        Guid orgId, [FromBody] CreateSpaceRequest req, CancellationToken ct)
    {
        await RequireManager(orgId, ct);

        if (!Enum.TryParse<BoardType>(req.BoardType, out var boardType))
            throw new InvalidOperationException("Invalid board type. Use 'Kanban' or 'Scrum'.");

        if (await db.Spaces.AnyAsync(s => s.OrgId == orgId && s.IssueKeyPrefix == req.IssueKeyPrefix.ToUpperInvariant(), ct))
            throw new InvalidOperationException("Issue key prefix is already in use in this organization.");

        var space = new Space
        {
            OrgId           = orgId,
            Name            = req.Name,
            BoardType       = boardType,
            IssueKeyPrefix  = req.IssueKeyPrefix.ToUpperInvariant(),
        };
        db.Spaces.Add(space);

        // Seed default workflow statuses and transitions
        SeedDefaultWorkflow(space, boardType);

        await db.SaveChangesAsync(ct);

        await db.Entry(space).Collection(s => s.WorkflowStatuses).LoadAsync(ct);
        return CreatedAtAction(nameof(GetSpace), new { orgId, spaceId = space.Id }, ToDto(space));
    }

    // ── PATCH /api/v1/orgs/{orgId}/spaces/{spaceId} ──────────────────────────

    [HttpPatch("{spaceId:guid}")]
    public async Task<ActionResult<SpaceDto>> UpdateSpace(
        Guid orgId, Guid spaceId, [FromBody] UpdateSpaceRequest req, CancellationToken ct)
    {
        await RequireManager(orgId, ct);
        var space = await LoadSpaceAsync(orgId, spaceId, ct);

        space.Name      = req.Name;
        space.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToDto(space);
    }

    // ── POST /api/v1/orgs/{orgId}/spaces/{spaceId}/statuses ──────────────────

    [HttpPost("{spaceId:guid}/statuses")]
    public async Task<ActionResult<WorkflowStatusDto>> AddStatus(
        Guid orgId, Guid spaceId, [FromBody] AddStatusRequest req, CancellationToken ct)
    {
        await RequireManager(orgId, ct);
        var space = await LoadSpaceAsync(orgId, spaceId, ct);

        // Shift existing statuses at or after the insertion position
        var toShift = await db.WorkflowStatuses
            .Where(w => w.SpaceId == space.Id && w.Position >= req.Position)
            .ToListAsync(ct);
        foreach (var ws in toShift) ws.Position++;

        var newStatus = new WorkflowStatus
        {
            SpaceId  = space.Id,
            Name     = req.Name,
            Color    = req.Color,
            Position = req.Position
        };
        db.WorkflowStatuses.Add(newStatus);
        await db.SaveChangesAsync(ct);

        return new WorkflowStatusDto(newStatus.Id, newStatus.Name, newStatus.Color,
            newStatus.Position, newStatus.IsInitial, newStatus.IsDoneStatus);
    }

    // ── PUT /api/v1/orgs/{orgId}/spaces/{spaceId}/statuses/{statusId} ────────

    [HttpPut("{spaceId:guid}/statuses/{statusId:guid}")]
    public async Task<ActionResult<WorkflowStatusDto>> UpdateStatus(
        Guid orgId, Guid spaceId, Guid statusId,
        [FromBody] UpdateStatusRequest req, CancellationToken ct)
    {
        await RequireManager(orgId, ct);
        await LoadSpaceAsync(orgId, spaceId, ct);

        var status = await db.WorkflowStatuses
            .FirstOrDefaultAsync(w => w.Id == statusId && w.SpaceId == spaceId, ct)
            ?? throw new KeyNotFoundException("Status not found in this space.");

        if (req.Name  != null) status.Name  = req.Name;
        if (req.Color != null) status.Color = req.Color;

        await db.SaveChangesAsync(ct);
        return new WorkflowStatusDto(status.Id, status.Name, status.Color,
            status.Position, status.IsInitial, status.IsDoneStatus);
    }

    // ── DELETE /api/v1/orgs/{orgId}/spaces/{spaceId}/statuses/{statusId} ─────

    [HttpDelete("{spaceId:guid}/statuses/{statusId:guid}")]
    public async Task<IActionResult> DeleteStatus(
        Guid orgId, Guid spaceId, Guid statusId, CancellationToken ct)
    {
        await RequireManager(orgId, ct);
        await LoadSpaceAsync(orgId, spaceId, ct);

        var status = await db.WorkflowStatuses
            .FirstOrDefaultAsync(w => w.Id == statusId && w.SpaceId == spaceId, ct)
            ?? throw new KeyNotFoundException("Status not found in this space.");

        if (await db.Issues.AnyAsync(i => i.StatusId == statusId && i.DeletedAt == null, ct))
            throw new InvalidOperationException(
                "Cannot delete a status that has active issues assigned to it.");

        db.WorkflowStatuses.Remove(status);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── PUT /api/v1/orgs/{orgId}/spaces/{spaceId}/statuses/reorder ───────────

    [HttpPut("{spaceId:guid}/statuses/reorder")]
    public async Task<ActionResult<List<WorkflowStatusDto>>> ReorderStatuses(
        Guid orgId, Guid spaceId, [FromBody] ReorderStatusesRequest req, CancellationToken ct)
    {
        await RequireManager(orgId, ct);
        await LoadSpaceAsync(orgId, spaceId, ct);

        var statuses = await db.WorkflowStatuses
            .Where(w => w.SpaceId == spaceId)
            .ToListAsync(ct);

        if (statuses.Count != req.OrderedStatusIds.Count ||
            statuses.Any(s => !req.OrderedStatusIds.Contains(s.Id)))
            throw new InvalidOperationException("OrderedStatusIds must contain exactly all status IDs for this space.");

        for (var i = 0; i < req.OrderedStatusIds.Count; i++)
        {
            var ws = statuses.First(s => s.Id == req.OrderedStatusIds[i]);
            ws.Position = i;
        }

        await db.SaveChangesAsync(ct);

        return statuses
            .OrderBy(s => s.Position)
            .Select(w => new WorkflowStatusDto(w.Id, w.Name, w.Color, w.Position, w.IsInitial, w.IsDoneStatus))
            .ToList();
    }

    // ── GET /api/v1/orgs/{orgId}/spaces/{spaceId}/transitions ────────────────

    [HttpGet("{spaceId:guid}/transitions")]
    public async Task<ActionResult<List<WorkflowTransitionDto>>> GetTransitions(
        Guid orgId, Guid spaceId, CancellationToken ct)
    {
        await RequireMember(orgId, ct);
        await LoadSpaceAsync(orgId, spaceId, ct);

        var txns = await db.WorkflowTransitions
            .Include(t => t.FromStatus)
            .Include(t => t.ToStatus)
            .Where(t => t.SpaceId == spaceId)
            .ToListAsync(ct);

        return txns.Select(ToTransitionDto).ToList();
    }

    // ── POST /api/v1/orgs/{orgId}/spaces/{spaceId}/transitions ───────────────

    [HttpPost("{spaceId:guid}/transitions")]
    public async Task<ActionResult<WorkflowTransitionDto>> AddTransition(
        Guid orgId, Guid spaceId, [FromBody] AddTransitionRequest req, CancellationToken ct)
    {
        await RequireManager(orgId, ct);
        await LoadSpaceAsync(orgId, spaceId, ct);

        if (await db.WorkflowTransitions.AnyAsync(
            t => t.SpaceId == spaceId &&
                 t.FromStatusId == req.FromStatusId &&
                 t.ToStatusId == req.ToStatusId, ct))
            throw new InvalidOperationException("This transition already exists.");

        var transition = new WorkflowTransition
        {
            SpaceId          = spaceId,
            FromStatusId     = req.FromStatusId,
            ToStatusId       = req.ToStatusId,
            AllowedRolesJson = req.AllowedRoles is { Count: > 0 }
                ? JsonSerializer.Serialize(req.AllowedRoles, _json)
                : null
        };
        db.WorkflowTransitions.Add(transition);
        await db.SaveChangesAsync(ct);

        await db.Entry(transition).Reference(t => t.FromStatus).LoadAsync(ct);
        await db.Entry(transition).Reference(t => t.ToStatus).LoadAsync(ct);

        return CreatedAtAction(nameof(GetTransitions), new { orgId, spaceId }, ToTransitionDto(transition));
    }

    // ── DELETE /api/v1/orgs/{orgId}/spaces/{spaceId}/transitions/{transitionId}

    [HttpDelete("{spaceId:guid}/transitions/{transitionId:guid}")]
    public async Task<IActionResult> DeleteTransition(
        Guid orgId, Guid spaceId, Guid transitionId, CancellationToken ct)
    {
        await RequireManager(orgId, ct);
        await LoadSpaceAsync(orgId, spaceId, ct);

        var transition = await db.WorkflowTransitions
            .FirstOrDefaultAsync(t => t.Id == transitionId && t.SpaceId == spaceId, ct)
            ?? throw new KeyNotFoundException("Transition not found.");

        db.WorkflowTransitions.Remove(transition);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Space> LoadSpaceAsync(Guid orgId, Guid spaceId, CancellationToken ct) =>
        await db.Spaces
            .Include(s => s.WorkflowStatuses.OrderBy(w => w.Position))
            .Include(s => s.Issues.Where(i => i.DeletedAt == null))
            .FirstOrDefaultAsync(s => s.Id == spaceId && s.OrgId == orgId, ct)
        ?? throw new KeyNotFoundException("Space not found.");

    private async Task RequireMember(Guid orgId, CancellationToken ct)
    {
        if (!await db.OrgMembers.AnyAsync(m => m.OrgId == orgId && m.UserId == CurrentUserId, ct))
            throw new UnauthorizedAccessException("You are not a member of this organization.");
    }

    private async Task RequireManager(Guid orgId, CancellationToken ct)
    {
        var member = await db.OrgMembers
            .FirstOrDefaultAsync(m => m.OrgId == orgId && m.UserId == CurrentUserId, ct)
            ?? throw new UnauthorizedAccessException("You are not a member of this organization.");
        if (member.Role != OrgRole.OrgManager)
            throw new UnauthorizedAccessException("Only OrgManagers can perform this action.");
    }

    private static SpaceDto ToDto(Space s) => new(
        s.Id, s.OrgId, s.Name,
        s.BoardType.ToString(),
        s.IssueKeyPrefix,
        s.Issues.Count,
        s.WorkflowStatuses
            .OrderBy(w => w.Position)
            .Select(w => new WorkflowStatusDto(w.Id, w.Name, w.Color, w.Position, w.IsInitial, w.IsDoneStatus))
            .ToList(),
        s.CreatedAt);

    private static WorkflowTransitionDto ToTransitionDto(WorkflowTransition t)
    {
        List<string> roles = [];
        if (!string.IsNullOrWhiteSpace(t.AllowedRolesJson))
        {
            try { roles = JsonSerializer.Deserialize<List<string>>(t.AllowedRolesJson) ?? []; }
            catch { /* ignore */ }
        }
        return new WorkflowTransitionDto(
            t.Id, t.FromStatusId, t.FromStatus.Name,
            t.ToStatusId, t.ToStatus.Name, roles);
    }

    /// <summary>Seeds default workflow for a newly created space.</summary>
    private static void SeedDefaultWorkflow(Space space, BoardType boardType)
    {
        if (boardType == BoardType.Scrum)
        {
            var todo       = new WorkflowStatus { SpaceId = space.Id, Name = "To Do",       Color = "#6B7280", Position = 0, IsInitial = true  };
            var inProgress = new WorkflowStatus { SpaceId = space.Id, Name = "In Progress",  Color = "#3B82F6", Position = 1 };
            var inReview   = new WorkflowStatus { SpaceId = space.Id, Name = "In Review",    Color = "#F59E0B", Position = 2 };
            var done       = new WorkflowStatus { SpaceId = space.Id, Name = "Done",         Color = "#10B981", Position = 3, IsDoneStatus = true };

            space.WorkflowStatuses.Add(todo);
            space.WorkflowStatuses.Add(inProgress);
            space.WorkflowStatuses.Add(inReview);
            space.WorkflowStatuses.Add(done);

            space.WorkflowTransitions.Add(new WorkflowTransition { SpaceId = space.Id, FromStatus = todo,       ToStatus = inProgress });
            space.WorkflowTransitions.Add(new WorkflowTransition { SpaceId = space.Id, FromStatus = inProgress, ToStatus = inReview   });
            space.WorkflowTransitions.Add(new WorkflowTransition { SpaceId = space.Id, FromStatus = inReview,   ToStatus = done        });
            space.WorkflowTransitions.Add(new WorkflowTransition { SpaceId = space.Id, FromStatus = inReview,   ToStatus = inProgress  });
            space.WorkflowTransitions.Add(new WorkflowTransition { SpaceId = space.Id, FromStatus = done,       ToStatus = inProgress  });
        }
        else // Kanban
        {
            var todo       = new WorkflowStatus { SpaceId = space.Id, Name = "To Do",      Color = "#6B7280", Position = 0, IsInitial = true };
            var inProgress = new WorkflowStatus { SpaceId = space.Id, Name = "In Progress", Color = "#3B82F6", Position = 1 };
            var done       = new WorkflowStatus { SpaceId = space.Id, Name = "Done",        Color = "#10B981", Position = 2, IsDoneStatus = true };

            space.WorkflowStatuses.Add(todo);
            space.WorkflowStatuses.Add(inProgress);
            space.WorkflowStatuses.Add(done);

            space.WorkflowTransitions.Add(new WorkflowTransition { SpaceId = space.Id, FromStatus = todo,       ToStatus = inProgress });
            space.WorkflowTransitions.Add(new WorkflowTransition { SpaceId = space.Id, FromStatus = inProgress, ToStatus = done       });
            space.WorkflowTransitions.Add(new WorkflowTransition { SpaceId = space.Id, FromStatus = done,       ToStatus = inProgress });
        }
    }
}
