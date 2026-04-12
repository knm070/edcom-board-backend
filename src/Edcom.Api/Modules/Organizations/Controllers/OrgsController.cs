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
[Route("api/v1/orgs")]
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
            .Where(m => m.UserId == userId && m.Organization.IsActive)
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

    // ── POST /api/v1/orgs  ────────────────────────────────────────────────────
    [HttpPost]
    [Authorize(Policy = EdcomPolicies.SystemAdmin)]   // Admin only
    public async Task<ActionResult<OrgDto>> Create(
        [FromBody] CreateOrgRequest req, CancellationToken ct)
    {
        var userId = CurrentUserId;
        var slug = GenerateSlug(req.Name);

        if (await db.Organizations.AnyAsync(o => o.Slug == slug, ct))
            slug = $"{slug}-{Guid.NewGuid().ToString()[..6]}";

        var org = new Organization
        {
            Name = req.Name,
            Slug = slug,
            LogoUrl = req.LogoUrl,
            CreatedById = userId
        };
        db.Organizations.Add(org);

        // Creator becomes OrgTaskManager
        db.OrgMembers.Add(new OrgMember
        {
            OrgId = org.Id,
            UserId = userId,
            Role = MemberRole.OrgTaskManager
        });

        // Generate unique prefixes: internal ends with I, external ends with X
        // Take up to 3 letters from slug's word-only chars, then append I/X
        var slugLetters = new string(slug.Where(char.IsLetter).ToArray()).ToUpper();
        var basePrefix  = slugLetters[..Math.Min(3, slugLetters.Length)];
        if (basePrefix.Length == 0) basePrefix = "ORG";

        var internalPrefix = await UniquePrefix(basePrefix + "I", ct);
        var externalPrefix = await UniquePrefix(basePrefix + "X", ct);

        // Seed internal + external spaces
        var internalSpace = new Space
        {
            OrgId = org.Id,
            Name = $"{req.Name} Internal",
            Type = SpaceType.Internal,
            BoardTemplate = BoardTemplate.Scrum,
            IssueKeyPrefix = internalPrefix
        };
        var externalSpace = new Space
        {
            OrgId = org.Id,
            Name = $"{req.Name} External",
            Type = SpaceType.External,
            BoardTemplate = BoardTemplate.Kanban,
            IssueKeyPrefix = externalPrefix
        };
        db.Spaces.AddRange(internalSpace, externalSpace);

        // Seed internal workflow statuses
        var internalStatuses = new[]
        {
            new WorkflowStatus { SpaceId = internalSpace.Id, Name = "Backlog",     Color = "#6B7280", Position = 0, IsInitial = true,  IsTerminal = false },
            new WorkflowStatus { SpaceId = internalSpace.Id, Name = "To Do",       Color = "#3B82F6", Position = 1, IsInitial = false, IsTerminal = false },
            new WorkflowStatus { SpaceId = internalSpace.Id, Name = "In Progress", Color = "#F59E0B", Position = 2, IsInitial = false, IsTerminal = false },
            new WorkflowStatus { SpaceId = internalSpace.Id, Name = "In Review",   Color = "#8B5CF6", Position = 3, IsInitial = false, IsTerminal = false },
            new WorkflowStatus { SpaceId = internalSpace.Id, Name = "Done",        Color = "#10B981", Position = 4, IsInitial = false, IsTerminal = true  },
        };
        db.WorkflowStatuses.AddRange(internalStatuses);

        // Seed external (system) workflow statuses — check if already exist
        var hasExternal = await db.WorkflowStatuses.AnyAsync(w => w.SpaceId == null, ct);
        if (!hasExternal)
        {
            db.WorkflowStatuses.AddRange(
                new WorkflowStatus { SpaceId = null, Name = "Backlog",     Color = "#6B7280", Position = 0, IsInitial = true,  IsTerminal = false },
                new WorkflowStatus { SpaceId = null, Name = "To Do",       Color = "#3B82F6", Position = 1, IsInitial = false, IsTerminal = false },
                new WorkflowStatus { SpaceId = null, Name = "In Progress", Color = "#F59E0B", Position = 2, IsInitial = false, IsTerminal = false },
                new WorkflowStatus { SpaceId = null, Name = "In Review",   Color = "#8B5CF6", Position = 3, IsInitial = false, IsTerminal = false },
                new WorkflowStatus { SpaceId = null, Name = "Done",        Color = "#10B981", Position = 4, IsInitial = false, IsTerminal = true  }
            );
        }

        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { orgId = org.Id }, new OrgDto(
            org.Id, org.Name, org.Slug, org.LogoUrl, MemberRole.OrgTaskManager.ToString(), 1, 2, org.CreatedAt));
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
            .FirstOrDefaultAsync(m => m.OrgId == orgId && m.UserId == userId && m.Organization.IsActive, ct)
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

        org.Name = req.Name;
        org.LogoUrl = req.LogoUrl;
        org.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var memberCount = await db.OrgMembers.CountAsync(m => m.OrgId == orgId, ct);
        var spaceCount  = await db.Spaces.CountAsync(s => s.OrgId == orgId, ct);
        return new OrgDto(org.Id, org.Name, org.Slug, org.LogoUrl,
            MemberRole.OrgTaskManager.ToString(), memberCount, spaceCount, org.CreatedAt);
    }

    // ── DELETE /api/v1/orgs/{orgId}  ──────────────────────────────────────────
    [HttpDelete("{orgId:guid}")]
    [Authorize(Policy = EdcomPolicies.SystemAdmin)]   // Admin only
    public async Task<IActionResult> Delete(Guid orgId, CancellationToken ct)
    {
        var org = await db.Organizations.FindAsync([orgId], ct)
            ?? throw new KeyNotFoundException("Organization not found.");
        org.IsActive = false;
        org.UpdatedAt = DateTime.UtcNow;
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

        if (!Enum.TryParse<MemberRole>(req.Role, out var role))
            throw new InvalidOperationException("Invalid role. Use 'OrgTaskManager' or 'Employer'.");

        var member = new OrgMember
        {
            OrgId = orgId,
            UserId = user.Id,
            Role = role,
            InvitedById = CurrentUserId
        };
        db.OrgMembers.Add(member);
        await db.SaveChangesAsync(ct);

        return new OrgMemberDto(user.Id, user.FullName, user.Email, user.AvatarUrl,
            role.ToString(), member.JoinedAt);
    }

    // ── PATCH /api/v1/orgs/{orgId}/members/{userId}  ─────────────────────────
    [HttpPatch("{orgId:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> UpdateMemberRole(
        Guid orgId, Guid memberId, [FromBody] UpdateMemberRoleRequest req, CancellationToken ct)
    {
        RequirePermission(perms.CanManageMembers(User, orgId));

        if (!Enum.TryParse<MemberRole>(req.Role, out var role))
            throw new InvalidOperationException("Invalid role.");

        var member = await db.OrgMembers
            .FirstOrDefaultAsync(m => m.OrgId == orgId && m.UserId == memberId, ct)
            ?? throw new KeyNotFoundException("Member not found.");

        member.Role = role;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── DELETE /api/v1/orgs/{orgId}/members/{userId}  ────────────────────────
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

    /// <summary>Throws 401 if the caller does not satisfy the permission check.</summary>
    private void RequirePermission(bool allowed, string message = "You do not have permission to perform this action.")
    {
        if (!allowed) throw new UnauthorizedAccessException(message);
    }

    /// <summary>Verifies membership via DB (for endpoints that pre-date the RBAC claims).</summary>
    private async Task RequireMembership(Guid orgId, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (!await db.OrgMembers.AnyAsync(m => m.OrgId == orgId && m.UserId == userId, ct))
            throw new UnauthorizedAccessException("You are not a member of this organization.");
    }

    private static string GenerateSlug(string name) =>
        Regex.Replace(name.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-").Trim('-');

    private async Task<string> UniquePrefix(string candidate, CancellationToken ct)
    {
        var prefix = candidate;
        var counter = 2;
        while (await db.Spaces.AnyAsync(s => s.IssueKeyPrefix == prefix, ct))
            prefix = candidate + counter++;
        return prefix;
    }
}
