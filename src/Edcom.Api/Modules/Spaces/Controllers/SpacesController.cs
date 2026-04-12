using System.Security.Claims;
using Edcom.Api.Infrastructure.Data;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Spaces.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Edcom.Api.Modules.Spaces.Controllers;

[ApiController]
[Route("api/v1/orgs/{orgId:guid}/spaces")]
[Authorize]
public class SpacesController(AppDbContext db) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    // ── GET /api/v1/orgs/{orgId}/spaces  ──────────────────────────────────────
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

    // ── GET /api/v1/orgs/{orgId}/spaces/{spaceId}  ────────────────────────────
    [HttpGet("{spaceId:guid}")]
    public async Task<ActionResult<SpaceDto>> GetSpace(Guid orgId, Guid spaceId, CancellationToken ct)
    {
        await RequireMember(orgId, ct);
        var space = await db.Spaces
            .Include(s => s.WorkflowStatuses.OrderBy(w => w.Position))
            .Include(s => s.Issues.Where(i => i.DeletedAt == null))
            .FirstOrDefaultAsync(s => s.Id == spaceId && s.OrgId == orgId, ct)
            ?? throw new KeyNotFoundException("Space not found.");

        return ToDto(space);
    }

    // ── PATCH /api/v1/orgs/{orgId}/spaces/{spaceId}  ──────────────────────────
    [HttpPatch("{spaceId:guid}")]
    public async Task<ActionResult<SpaceDto>> UpdateSpace(
        Guid orgId, Guid spaceId, [FromBody] UpdateSpaceRequest req, CancellationToken ct)
    {
        await RequireManager(orgId, ct);
        var space = await db.Spaces
            .Include(s => s.WorkflowStatuses.OrderBy(w => w.Position))
            .Include(s => s.Issues.Where(i => i.DeletedAt == null))
            .FirstOrDefaultAsync(s => s.Id == spaceId && s.OrgId == orgId, ct)
            ?? throw new KeyNotFoundException("Space not found.");

        space.Name = req.Name;
        if (req.BoardTemplate != null && Enum.TryParse<BoardTemplate>(req.BoardTemplate, out var tmpl))
            space.BoardTemplate = tmpl;
        space.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToDto(space);
    }

    private static SpaceDto ToDto(Space s) => new(
        s.Id, s.OrgId, s.Name, s.Type.ToString(), s.BoardTemplate?.ToString(),
        s.IssueKeyPrefix,
        s.Issues.Count,
        s.WorkflowStatuses.Select(w => new WorkflowStatusDto(
            w.Id, w.Name, w.Color, w.Position, w.IsInitial, w.IsTerminal)).ToList(),
        s.CreatedAt);

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
        if (member.Role != MemberRole.OrgTaskManager)
            throw new UnauthorizedAccessException("Only OrgTaskManagers can modify spaces.");
    }
}
