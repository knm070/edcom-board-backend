using Edcom.Api.Infrastructure.Data;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Authorization.Extensions;
using Edcom.Api.Modules.Authorization.Policies;
using Edcom.Api.Modules.Users.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Edcom.Api.Modules.Users.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = EdcomPolicies.SystemAdminOnly)]
public class UsersController(AppDbContext db) : ControllerBase
{
    private Guid CurrentUserId => User.GetUserId();

    // ── GET /api/users ────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<List<AdminUserDto>>> List(CancellationToken ct)
    {
        return await db.Users
            .OrderBy(u => u.CreatedAt)
            .Select(u => new AdminUserDto(
                u.Id,
                u.Email,
                u.FullName,
                u.AvatarUrl,
                u.IsSystemAdmin,
                u.IsActive,
                u.OrgMemberships.Count,
                u.CreatedAt))
            .ToListAsync(ct);
    }

    // ── GET /api/users/{userId} ───────────────────────────────────────────────
    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<AdminUserDto>> GetById(Guid userId, CancellationToken ct)
    {
        return await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new AdminUserDto(
                u.Id,
                u.Email,
                u.FullName,
                u.AvatarUrl,
                u.IsSystemAdmin,
                u.IsActive,
                u.OrgMemberships.Count,
                u.CreatedAt))
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("User not found.");
    }

    // ── POST /api/users ───────────────────────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<CreateUserResponse>> Create(
        [FromBody] CreateUserRequest req, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.Email == req.Email, ct))
            throw new InvalidOperationException("A user with this email already exists.");

        var user = new User
        {
            Email         = req.Email,
            FullName      = req.FullName,
            PasswordHash  = BCrypt.Net.BCrypt.HashPassword(req.Password),
            IsSystemAdmin = req.IsSystemAdmin,
            IsActive      = true,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        var dto = new AdminUserDto(
            user.Id, user.Email, user.FullName, user.AvatarUrl,
            user.IsSystemAdmin, user.IsActive, 0, user.CreatedAt);

        return CreatedAtAction(
            nameof(GetById),
            new { userId = user.Id },
            new CreateUserResponse(dto, req.Password));
    }

    // ── PATCH /api/users/{userId} ─────────────────────────────────────────────
    [HttpPatch("{userId:guid}")]
    public async Task<ActionResult<AdminUserDto>> Update(
        Guid userId, [FromBody] UpdateUserRequest req, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([userId], ct)
            ?? throw new KeyNotFoundException("User not found.");

        // Guard: admin cannot demote or deactivate themselves
        if (userId == CurrentUserId && (req.IsActive == false || req.IsSystemAdmin == false))
            throw new InvalidOperationException("You cannot deactivate or demote your own account.");

        // Guard: email must be unique if changed
        if (req.Email is not null && req.Email != user.Email
            && await db.Users.AnyAsync(u => u.Email == req.Email, ct))
            throw new InvalidOperationException("A user with this email already exists.");

        if (req.FullName      is not null) user.FullName      = req.FullName;
        if (req.Email         is not null) user.Email         = req.Email;
        if (req.AvatarUrl     is not null) user.AvatarUrl     = req.AvatarUrl;
        if (req.IsActive      is not null) user.IsActive      = req.IsActive.Value;
        if (req.IsSystemAdmin is not null) user.IsSystemAdmin = req.IsSystemAdmin.Value;

        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var orgCount = await db.OrgMembers.CountAsync(m => m.UserId == userId, ct);
        return new AdminUserDto(
            user.Id, user.Email, user.FullName, user.AvatarUrl,
            user.IsSystemAdmin, user.IsActive, orgCount, user.CreatedAt);
    }

    // ── DELETE /api/users/{userId} ────────────────────────────────────────────
    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Deactivate(Guid userId, CancellationToken ct)
    {
        if (userId == CurrentUserId)
            throw new InvalidOperationException("You cannot deactivate your own account.");

        var user = await db.Users.FindAsync([userId], ct)
            ?? throw new KeyNotFoundException("User not found.");

        user.IsActive  = false;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
