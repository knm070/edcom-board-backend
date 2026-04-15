using System.Text.RegularExpressions;
using Edcom.Api.Infrastructure.Data;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Authorization.Extensions;
using Edcom.Api.Modules.Authorization.Policies;
using Edcom.Api.Modules.Organizations.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Edcom.Api.Modules.Organizations.Controllers;

[ApiController]
[Route("api/orgs")]
[Authorize]
public class OrgsController(AppDbContext db) : ControllerBase
{
    private Guid CurrentUserId => User.GetUserId();

    // ── GET /api/orgs  ─────────────────────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<List<OrgDto>>> GetMyOrgs(CancellationToken ct)
    {
        var userId = CurrentUserId;
        var memberships = await db.OrgMembers
            .Include(m => m.Organization)
                .ThenInclude(o => o.Members)
            .Include(m => m.Organization)
                .ThenInclude(o => o.Spaces)
            .Where(m => m.UserId == userId
                     && m.Organization.IsActive
                     && m.Organization.Status == OrgStatus.Active)
            .ToListAsync(ct);

        return memberships.Select(m => new OrgDto(
            m.Organization.Id,
            m.Organization.Name,
            m.Organization.Slug,
            m.Organization.LogoUrl,
            m.Role.ToString(),
            m.Organization.Members.Count,
            m.Organization.Spaces.Count,
            m.Organization.CreatedAt
        )).ToList();
    }

    // ── POST /api/orgs/request  ────────────────────────────────────────────
    // Any authenticated user can request org creation.
    [HttpPost("request")]
    public async Task<ActionResult<OrgDto>> RequestCreate(
        [FromBody] CreateOrgRequest req, CancellationToken ct)
    {
        var userId = CurrentUserId;
        var slug = GenerateSlug(req.Name);

        if (await db.Organizations.AnyAsync(o => o.Slug == slug, ct))
            slug = $"{slug}-{Guid.NewGuid().ToString()[..6]}";

        var org = new Organization
        {
            Name                = req.Name,
            Slug                = slug,
            LogoUrl             = req.LogoUrl,
            CreatedById         = userId,
            Status              = OrgStatus.Pending,
            BoardTypePreference = req.BoardTypePreference ?? "Kanban",
        };
        db.Organizations.Add(org);

        // Auto-attach the requesting user as OrgManager immediately
        db.OrgMembers.Add(new OrgMember
        {
            OrgId  = org.Id,
            UserId = userId,
            Role   = OrgRole.OrgManager,
        });

        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { orgId = org.Id }, new OrgDto(
            org.Id, org.Name, org.Slug, org.LogoUrl, string.Empty, 0, 0, org.CreatedAt));
    }

    // ── GET /api/orgs/{orgId}  ─────────────────────────────────────────────
    [HttpGet("{orgId:guid}")]
    public async Task<ActionResult<OrgDto>> GetById(Guid orgId, CancellationToken ct)
    {
        var userId = CurrentUserId;
        var membership = await db.OrgMembers
            .Include(m => m.Organization)
                .ThenInclude(o => o.Members)
            .Include(m => m.Organization)
                .ThenInclude(o => o.Spaces)
            .FirstOrDefaultAsync(m => m.OrgId == orgId && m.UserId == userId
                                   && m.Organization.IsActive
                                   && m.Organization.Status == OrgStatus.Active, ct)
            ?? throw new KeyNotFoundException("Organization not found or you are not a member.");

        var org = membership.Organization;
        return new OrgDto(org.Id, org.Name, org.Slug, org.LogoUrl, membership.Role.ToString(),
            org.Members.Count, org.Spaces.Count, org.CreatedAt);
    }

    // ── GET /api/orgs/my-requests  ────────────────────────────────────────────
    // Returns all org creation requests submitted by the current user (any status).
    [HttpGet("my-requests")]
    public async Task<ActionResult<List<MyOrgRequestDto>>> GetMyRequests(CancellationToken ct)
    {
        var userId = CurrentUserId;
        var requests = await db.Organizations
            .Where(o => o.CreatedById == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new MyOrgRequestDto(
                o.Id,
                o.Name,
                o.Slug,
                o.LogoUrl,
                o.Status.ToString(),
                o.RejectionReason,
                o.CreatedAt))
            .ToListAsync(ct);

        return requests;
    }

    // ── PATCH /api/v1/orgs/{orgId}  ───────────────────────────────────────────
    [HttpPatch("{orgId:guid}")]
    public async Task<ActionResult<OrgDto>> Update(
        Guid orgId, [FromBody] UpdateOrgRequest req, CancellationToken ct)
    {
        await RequireOrgManagerAsync(orgId, ct);
        var org = await db.Organizations.FindAsync([orgId], ct)
            ?? throw new KeyNotFoundException("Organization not found.");

        org.Name    = req.Name;
        org.LogoUrl = req.LogoUrl;
        org.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var memberCount = await db.OrgMembers.CountAsync(m => m.OrgId == orgId, ct);
        var spaceCount  = await db.Spaces.CountAsync(s => s.OrgId == orgId, ct);
        var currentRole = User.GetOrgRole(orgId)?.ToString() ?? OrgRole.OrgManager.ToString();
        return new OrgDto(org.Id, org.Name, org.Slug, org.LogoUrl,
            currentRole, memberCount, spaceCount, org.CreatedAt);
    }

    // ── DELETE /api/v1/orgs/{orgId}  ──────────────────────────────────────────
    [HttpDelete("{orgId:guid}")]
    [Authorize(Policy = EdcomPolicies.SystemAdminOnly)]
    public async Task<IActionResult> Delete(Guid orgId, CancellationToken ct)
    {
        var org = await db.Organizations.FindAsync([orgId], ct)
            ?? throw new KeyNotFoundException("Organization not found.");
        org.IsActive  = false;
        org.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── GET /api/orgs/admin/all  ──────────────────────────────────────────────
    [HttpGet("admin/all")]
    [Authorize(Policy = EdcomPolicies.SystemAdminOnly)]
    public async Task<ActionResult<List<AdminOrgListDto>>> GetAllOrgs(CancellationToken ct)
    {
        var orgs = await db.Organizations
            .Include(o => o.Members)
            .Include(o => o.Spaces)
                .ThenInclude(s => s.Issues.Where(i => i.DeletedAt == null))
            .Where(o => o.IsActive && o.Status == OrgStatus.Active)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        return orgs.Select(o => new AdminOrgListDto(
            o.Id, o.Name, o.Slug, o.LogoUrl,
            o.Members.Count,
            o.Spaces.Count,
            o.Spaces.Sum(s => s.Issues.Count),
            o.CreatedAt
        )).ToList();
    }

    // ── GET /api/orgs/admin/requests  ─────────────────────────────────────────
    [HttpGet("admin/requests")]
    [Authorize(Policy = EdcomPolicies.SystemAdminOnly)]
    public async Task<ActionResult<List<AdminOrgRequestListDto>>> GetAllRequests(CancellationToken ct)
    {
        var requests = await db.Organizations
            .Include(o => o.CreatedBy)
            .Where(o => o.IsActive)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        return requests.Select(o => new AdminOrgRequestListDto(
            o.Id, o.Name, o.Slug, o.LogoUrl,
            o.Status.ToString(), o.RejectionReason,
            o.CreatedById, o.CreatedBy.FullName, o.CreatedBy.Email,
            o.CreatedAt
        )).ToList();
    }

    // ── GET /api/v1/orgs/admin/pending  ───────────────────────────────────────
    [HttpGet("admin/pending")]
    [Authorize(Policy = EdcomPolicies.SystemAdminOnly)]
    public async Task<ActionResult<List<OrgPendingDto>>> GetPendingOrgs(CancellationToken ct)
    {
        var pending = await db.Organizations
            .Include(o => o.CreatedBy)
            .Where(o => o.Status == OrgStatus.Pending && o.IsActive)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(ct);

        return pending.Select(o => new OrgPendingDto(
            o.Id, o.Name, o.Slug, o.LogoUrl,
            o.CreatedById, o.CreatedBy.FullName, o.CreatedBy.Email,
            o.CreatedAt)).ToList();
    }

    // ── POST /api/v1/orgs/admin/{orgId}/approve  ──────────────────────────────
    [HttpPost("admin/{orgId:guid}/approve")]
    [Authorize(Policy = EdcomPolicies.SystemAdminOnly)]
    public async Task<ActionResult<OrgDto>> ApproveOrg(Guid orgId, CancellationToken ct)
    {
        var org = await db.Organizations.FindAsync([orgId], ct)
            ?? throw new KeyNotFoundException("Organization not found.");

        if (org.Status != OrgStatus.Pending)
            throw new InvalidOperationException("Only Pending organizations can be approved.");

        org.Status    = OrgStatus.Active;
        org.UpdatedAt = DateTime.UtcNow;

        // Ensure creator is an OrgManager (they are attached on request creation, but guard anyway)
        if (!await db.OrgMembers.AnyAsync(m => m.OrgId == orgId && m.UserId == org.CreatedById, ct))
        {
            db.OrgMembers.Add(new OrgMember
            {
                OrgId  = orgId,
                UserId = org.CreatedById,
                Role   = OrgRole.OrgManager,
            });
        }

        // Auto-create the space with the preferred board type (if none exists yet)
        if (!await db.Spaces.AnyAsync(s => s.OrgId == orgId, ct))
        {
            if (!Enum.TryParse<BoardType>(org.BoardTypePreference, out var boardType))
                boardType = BoardType.Kanban;

            var prefix = org.Slug.Length >= 3
                ? org.Slug[..3].ToUpperInvariant()
                : org.Slug.ToUpperInvariant();
            var candidate = prefix;
            var i = 0;
            while (await db.Spaces.AnyAsync(s => s.IssueKeyPrefix == candidate, ct))
                candidate = $"{prefix}{++i}";

            var space = new Space
            {
                OrgId          = orgId,
                Name           = $"{org.Name} Board",
                BoardType      = boardType,
                IssueKeyPrefix = candidate,
            };
            db.Spaces.Add(space);
            SeedDefaultWorkflow(space, boardType);
        }

        await db.SaveChangesAsync(ct);
        return new OrgDto(org.Id, org.Name, org.Slug, org.LogoUrl,
            OrgRole.OrgManager.ToString(), 1, 0, org.CreatedAt);
    }

    // ── POST /api/v1/orgs/admin/{orgId}/reject  ───────────────────────────────
    [HttpPost("admin/{orgId:guid}/reject")]
    [Authorize(Policy = EdcomPolicies.SystemAdminOnly)]
    public async Task<IActionResult> RejectOrg(
        Guid orgId, [FromBody] RejectOrgRequest req, CancellationToken ct)
    {
        var org = await db.Organizations.FindAsync([orgId], ct)
            ?? throw new KeyNotFoundException("Organization not found.");

        if (org.Status != OrgStatus.Pending)
            throw new InvalidOperationException("Only Pending organizations can be rejected.");

        org.Status          = OrgStatus.Rejected;
        org.RejectionReason = req.Reason;
        org.UpdatedAt       = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── GET /api/v1/orgs/{orgId}/members  ─────────────────────────────────────
    [HttpGet("{orgId:guid}/members")]
    public async Task<ActionResult<List<OrgMemberDto>>> GetMembers(Guid orgId, CancellationToken ct)
    {
        await RequireOrgMemberAsync(orgId, ct);
        return await db.OrgMembers
            .Include(m => m.User)
            .Where(m => m.OrgId == orgId)
            .OrderBy(m => m.JoinedAt)
            .Select(m => new OrgMemberDto(
                m.UserId, m.User.FullName, m.User.Email, m.User.AvatarUrl,
                m.Role.ToString(), m.JoinedAt))
            .ToListAsync(ct);
    }

    // ── POST /api/v1/orgs/{orgId}/members  ────────────────────────────────────
    [HttpPost("{orgId:guid}/members")]
    public async Task<ActionResult<OrgMemberDto>> InviteMember(
        Guid orgId, [FromBody] InviteMemberRequest req, CancellationToken ct)
    {
        await RequireOrgManagerAsync(orgId, ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email, ct)
            ?? throw new KeyNotFoundException("User not found. They must register first.");

        if (await db.OrgMembers.AnyAsync(m => m.OrgId == orgId && m.UserId == user.Id, ct))
            throw new InvalidOperationException("User is already a member of this organization.");

        if (!Enum.TryParse<OrgRole>(req.Role, out var role))
            throw new InvalidOperationException("Invalid role. Use 'OrgManager', 'SpaceManager', or 'Employer'.");

        var member = new OrgMember
        {
            OrgId       = orgId,
            UserId      = user.Id,
            Role        = role,
            InvitedById = CurrentUserId
        };
        db.OrgMembers.Add(member);
        await db.SaveChangesAsync(ct);

        return new OrgMemberDto(user.Id, user.FullName, user.Email, user.AvatarUrl,
            role.ToString(), member.JoinedAt);
    }

    // ── PATCH /api/v1/orgs/{orgId}/members/{memberId}  ───────────────────────
    [HttpPatch("{orgId:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> UpdateMemberRole(
        Guid orgId, Guid memberId, [FromBody] UpdateMemberRoleRequest req, CancellationToken ct)
    {
        await RequireOrgManagerAsync(orgId, ct);

        if (!Enum.TryParse<OrgRole>(req.Role, out var role))
            throw new InvalidOperationException("Invalid role.");

        var member = await db.OrgMembers
            .FirstOrDefaultAsync(m => m.OrgId == orgId && m.UserId == memberId, ct)
            ?? throw new KeyNotFoundException("Member not found.");

        member.Role = role;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── DELETE /api/v1/orgs/{orgId}/members/{memberId}  ──────────────────────
    [HttpDelete("{orgId:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> RemoveMember(
        Guid orgId, Guid memberId, CancellationToken ct)
    {
        await RequireOrgManagerAsync(orgId, ct);

        var member = await db.OrgMembers
            .FirstOrDefaultAsync(m => m.OrgId == orgId && m.UserId == memberId, ct)
            ?? throw new KeyNotFoundException("Member not found.");

        db.OrgMembers.Remove(member);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Helpers  ──────────────────────────────────────────────────────────────

    private async Task RequireOrgMemberAsync(Guid orgId, CancellationToken ct)
    {
        if (User.IsSystemAdmin()) return;
        if (!await db.OrgMembers.AnyAsync(m => m.OrgId == orgId && m.UserId == CurrentUserId, ct))
            throw new UnauthorizedAccessException("You are not a member of this organization.");
    }

    private async Task RequireOrgManagerAsync(Guid orgId, CancellationToken ct)
    {
        if (User.IsSystemAdmin()) return;
        var member = await db.OrgMembers
            .FirstOrDefaultAsync(m => m.OrgId == orgId && m.UserId == CurrentUserId, ct);
        if (member is null || member.Role != OrgRole.OrgManager)
            throw new UnauthorizedAccessException("Only OrgManagers can perform this action.");
    }

    private static string GenerateSlug(string name) =>
        Regex.Replace(name.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-").Trim('-');

    private static void SeedDefaultWorkflow(Space space, BoardType boardType)
    {
        if (boardType == BoardType.Scrum)
        {
            var todo       = new WorkflowStatus { SpaceId = space.Id, Name = "To Do",       Color = "#6B7280", Position = 0, IsInitial = true };
            var inProgress = new WorkflowStatus { SpaceId = space.Id, Name = "In Progress", Color = "#3B82F6", Position = 1 };
            var inReview   = new WorkflowStatus { SpaceId = space.Id, Name = "In Review",   Color = "#F59E0B", Position = 2 };
            var done       = new WorkflowStatus { SpaceId = space.Id, Name = "Done",        Color = "#10B981", Position = 3, IsDoneStatus = true };

            space.WorkflowStatuses.Add(todo);
            space.WorkflowStatuses.Add(inProgress);
            space.WorkflowStatuses.Add(inReview);
            space.WorkflowStatuses.Add(done);

            space.WorkflowTransitions.Add(new WorkflowTransition { SpaceId = space.Id, FromStatus = todo,       ToStatus = inProgress });
            space.WorkflowTransitions.Add(new WorkflowTransition { SpaceId = space.Id, FromStatus = inProgress, ToStatus = inReview   });
            space.WorkflowTransitions.Add(new WorkflowTransition { SpaceId = space.Id, FromStatus = inReview,   ToStatus = done       });
            space.WorkflowTransitions.Add(new WorkflowTransition { SpaceId = space.Id, FromStatus = inReview,   ToStatus = inProgress });
            space.WorkflowTransitions.Add(new WorkflowTransition { SpaceId = space.Id, FromStatus = done,       ToStatus = inProgress });
        }
        else // Kanban
        {
            var todo       = new WorkflowStatus { SpaceId = space.Id, Name = "To Do",       Color = "#6B7280", Position = 0, IsInitial = true };
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
