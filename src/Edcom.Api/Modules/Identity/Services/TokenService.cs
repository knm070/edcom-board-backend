using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Authorization.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Edcom.Api.Modules.Identity.Services;

public interface ITokenService
{
    string GenerateAccessToken(
        User user,
        IEnumerable<OrgMember> memberships,
        IEnumerable<SpaceAssignment> spaceAssignments);
    string GenerateRefreshToken();
    string HashToken(string token);
    ClaimsPrincipal? ValidateExpiredToken(string token);
}

public class TokenService(IConfiguration config) : ITokenService
{
    private readonly string _key      = config["Jwt:Key"]      ?? "super-secret-dev-key-must-be-32-chars!";
    private readonly string _issuer   = config["Jwt:Issuer"]   ?? "edcom";
    private readonly string _audience = config["Jwt:Audience"] ?? "edcom-client";
    private readonly int    _expiryMinutes = int.Parse(config["Jwt:ExpiryMinutes"] ?? "60");

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── Access token ─────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a signed JWT containing:
    /// <list type="bullet">
    ///   <item><c>sub</c>         — user ID</item>
    ///   <item><c>email</c></item>
    ///   <item><c>full_name</c></item>
    ///   <item><c>is_admin</c>    — "true" | "false"</item>
    ///   <item><c>org_roles</c>   — JSON array of { orgId, orgSlug, role }</item>
    ///   <item><c>space_roles</c> — JSON array of { orgId, spaceId, spaceSlug } (SpaceManager entries only)</item>
    /// </list>
    /// </summary>
    public string GenerateAccessToken(
        User user,
        IEnumerable<OrgMember> memberships,
        IEnumerable<SpaceAssignment> spaceAssignments)
    {
        var orgRoles = memberships
            .Select(m => new OrgRoleClaim(
                m.OrgId,
                m.Organization?.Slug ?? string.Empty,
                m.Role.ToString()))
            .ToList();

        var spaceRoles = spaceAssignments
            .Select(sa => new SpaceRoleClaim(
                sa.Space?.OrgId ?? Guid.Empty,
                sa.SpaceId,
                sa.Space?.Name ?? string.Empty))
            .ToList();

        var claims = new List<Claim>
        {
            new(EdcomClaimTypes.UserId,     user.Id.ToString()),
            new(EdcomClaimTypes.Email,      user.Email),
            new(EdcomClaimTypes.FullName,   user.FullName),
            new(EdcomClaimTypes.IsAdmin,    user.IsSystemAdmin.ToString().ToLowerInvariant()),
            new(EdcomClaimTypes.OrgRoles,   JsonSerializer.Serialize(orgRoles,   _jsonOpts)),
            new(EdcomClaimTypes.SpaceRoles, JsonSerializer.Serialize(spaceRoles, _jsonOpts)),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             _issuer,
            audience:           _audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(_expiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ── Refresh token ─────────────────────────────────────────────────────────

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // ── Validation (refresh flow) ─────────────────────────────────────────────

    public ClaimsPrincipal? ValidateExpiredToken(string token)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = false,   // allow expired
            ValidateIssuerSigningKey = true,
            ValidIssuer              = _issuer,
            ValidAudience            = _audience,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key))
        };

        try { return new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _); }
        catch  { return null; }
    }
}
