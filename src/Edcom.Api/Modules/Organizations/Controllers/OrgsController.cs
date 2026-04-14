using System.Text.RegularExpressions;
using Edcom.Api.Infrastructure.Data;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Authorization.Extensions;
using Edcom.Api.Modules.Authorization.Policies;
using Edcom.Api.Modules.Authorization.Services;
using Edcom.Api.Modules.Organizations.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Edcom.Api.Modules.Organizations.Controllers;

[ApiController]
[Route("api/orgs")]
[Authorize]
public class OrgsController(AppDbContext db, IPermissionService perms) : ControllerBase
{
    private Guid CurrentUserId => User.GetUserId();

    // ── GET /api/v1/orgs  ─────────────────────────────────────────────────────
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

    // ── POST /api/v1/orgs/request  ────────────────────────────────────────────
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
            Name        = req.Name,
            Slug        = slug,
            LogoUrl     = req.LogoUrl,
            CreatedById = userId,
            Status      = OrgStatus.Pending,
        };
        db.Organizations.Add(org);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { orgId = org.Id }, new OrgDto(
            org.Id, org.Name, org.Slug, org.LogoUrl, string.Empty, 0, 0, org.CreatedAt));
    }

    // ── GET /api/v1/orgs/{orgId}  ─────────────────────────────────────────────
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

    // ── PATCH /api/v1/orgs/{orgId}  ───────────────────────────────────────────
    [HttpPatch("{orgId:guid}")]
    public async Task<ActionResult<OrgDto>> Update(
        Guid orgId, [FromBody] UpdateOrgRequest req, CancellationToken ct)
    {
        RequirePermission(perms.CanManageMembers(User, orgId));
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

        // Make the creator an OrgManager
        if (!await db.OrgMembers.AnyAsync(m => m.OrgId == orgId && m.UserId == org.CreatedById, ct))
        {
            db.OrgMembers.Add(new OrgMember
            {
                OrgId   = orgId,
                UserId  = org.CreatedById,
                Role    = OrgRole.OrgManager,
            });
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
        RequirePermission(perms.CanViewOrgMembers(User, orgId));
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
        RequirePermission(perms.CanManageMembers(User, orgId));

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
        RequirePermission(perms.CanManageMembers(User, orgId));

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
        RequirePermission(perms.CanManageMembers(User, orgId));

        var member = await db.OrgMembers
            .FirstOrDefaultAsync(m => m.OrgId == orgId && m.UserId == memberId, ct)
            ?? throw new KeyNotFoundException("Member not found.");

        db.OrgMembers.Remove(member);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Helpers  ──────────────────────────────────────────────────────────────

    private void RequirePermission(bool allowed, string message = "You do not have permission to perform this action.")
    {
        if (!allowed) throw new UnauthorizedAccessException(message);
    }

    private static string GenerateSlug(string name) =>
        Regex.Replace(name.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-").Trim('-');
}
