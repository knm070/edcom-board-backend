using Edcom.Api.Infrastructure.Data;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Identity.Dto;
using Microsoft.EntityFrameworkCore;

namespace Edcom.Api.Modules.Identity.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest req, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest req, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task RevokeAsync(string refreshToken, CancellationToken ct = default);
    Task<UserDto> GetMeAsync(Guid userId, CancellationToken ct = default);
}

public class AuthService(AppDbContext db, ITokenService tokens, IConfiguration config) : IAuthService
{
    private readonly int _refreshDays = int.Parse(config["Jwt:RefreshDays"] ?? "30");

    public async Task<AuthResponse> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.Email == req.Email.ToLower(), ct))
            throw new InvalidOperationException("Email already registered.");

        var user = new User
        {
            Email = req.Email.ToLower(),
            FullName = req.FullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return await IssueTokenPairAsync(user, [], ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var user = await db.Users
            .Include(u => u.OrgMemberships).ThenInclude(m => m.Organization)
            .FirstOrDefaultAsync(u => u.Email == req.Email.ToLower(), ct)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (user.PasswordHash is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is deactivated.");

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await IssueTokenPairAsync(user, user.OrgMemberships, ct);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = tokens.HashToken(refreshToken);
        var stored = await db.RefreshTokens
            .Include(rt => rt.User).ThenInclude(u => u.OrgMemberships).ThenInclude(m => m.Organization)
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash, ct)
            ?? throw new UnauthorizedAccessException("Refresh token not found.");

        if (!stored.IsActive)
            throw new UnauthorizedAccessException("Refresh token is expired or revoked.");

        // Rotate: revoke old, issue new
        stored.RevokedAt = DateTime.UtcNow;
        return await IssueTokenPairAsync(stored.User, stored.User.OrgMemberships, ct);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = tokens.HashToken(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == hash, ct);
        if (stored is not null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<UserDto> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users
            .Include(u => u.OrgMemberships).ThenInclude(m => m.Organization)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        return MapToDto(user, user.OrgMemberships);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<AuthResponse> IssueTokenPairAsync(User user, IEnumerable<OrgMember> memberships, CancellationToken ct)
    {
        var accessToken = tokens.GenerateAccessToken(user, memberships);
        var rawRefresh = tokens.GenerateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokens.HashToken(rawRefresh),
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshDays)
        });
        await db.SaveChangesAsync(ct);

        return new AuthResponse(
            User: MapToDto(user, memberships),
            AccessToken: accessToken,
            RefreshToken: rawRefresh,
            ExpiresIn: int.Parse(config["Jwt:ExpiryMinutes"] ?? "60") * 60
        );
    }

    private static UserDto MapToDto(User user, IEnumerable<OrgMember> memberships) =>
        new(
            user.Id,
            user.Email,
            user.FullName,
            user.AvatarUrl,
            user.IsSystemAdmin,
            memberships.Select(m => new OrgRoleDto(
                m.OrgId,
                m.Organization?.Name ?? "",
                m.Role.ToString(),
                m.JoinedAt
            ))
        );
}
