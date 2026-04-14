using System.Security.Claims;
using System.Text.Json;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Authorization.Claims;

namespace Edcom.Api.Modules.Authorization.Extensions;

public static class ClaimsPrincipalExtensions
{
    private static readonly JsonSerializerOptions _jsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    // ── Identity ──────────────────────────────────────────────────────────────

    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(EdcomClaimTypes.UserId)
               ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? throw new UnauthorizedAccessException("User ID claim not present.");
        return Guid.Parse(raw);
    }

    public static bool IsSystemAdmin(this ClaimsPrincipal principal)
        => string.Equals(
            principal.FindFirstValue(EdcomClaimTypes.IsAdmin),
            "true",
            StringComparison.OrdinalIgnoreCase);

    // ── Org-role extraction ───────────────────────────────────────────────────

    public static IReadOnlyList<OrgRoleClaim> GetOrgRoles(this ClaimsPrincipal principal)
    {
        var json = principal.FindFirstValue(EdcomClaimTypes.OrgRoles);
        if (string.IsNullOrWhiteSpace(json)) return [];
        return JsonSerializer.Deserialize<List<OrgRoleClaim>>(json, _jsonOpts) ?? [];
    }

    public static IReadOnlyList<SpaceRoleClaim> GetSpaceRoles(this ClaimsPrincipal principal)
    {
        var json = principal.FindFirstValue(EdcomClaimTypes.SpaceRoles);
        if (string.IsNullOrWhiteSpace(json)) return [];
        return JsonSerializer.Deserialize<List<SpaceRoleClaim>>(json, _jsonOpts) ?? [];
    }

    // ── Org-scoped role checks ────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the user is a SystemAdmin OR holds at least one of the
    /// specified <paramref name="roles"/> in <paramref name="orgId"/>.
    /// </summary>
    public static bool HasOrgRole(this ClaimsPrincipal principal, Guid orgId, params OrgRole[] roles)
    {
        if (principal.IsSystemAdmin()) return true;
        return principal.GetOrgRoles().Any(r =>
            r.OrgId == orgId &&
            roles.Any(allowed => string.Equals(allowed.ToString(), r.Role, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Returns true if the user holds the given role in ANY org (or is SystemAdmin).</summary>
    public static bool HasAnyOrgRole(this ClaimsPrincipal principal, params OrgRole[] roles)
    {
        if (principal.IsSystemAdmin()) return true;
        return principal.GetOrgRoles().Any(r =>
            roles.Any(allowed => string.Equals(allowed.ToString(), r.Role, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Returns true if the user is a member (any role) of the given org, or is SystemAdmin.</summary>
    public static bool IsMemberOfOrg(this ClaimsPrincipal principal, Guid orgId)
    {
        if (principal.IsSystemAdmin()) return true;
        return principal.GetOrgRoles().Any(r => r.OrgId == orgId);
    }

    /// <summary>
    /// Returns the <see cref="OrgRole"/> the user holds in <paramref name="orgId"/>,
    /// or <c>null</c> if they are not a member.
    /// SystemAdmins are treated as <see cref="OrgRole.OrgManager"/>.
    /// </summary>
    public static OrgRole? GetOrgRole(this ClaimsPrincipal principal, Guid orgId)
    {
        if (principal.IsSystemAdmin()) return OrgRole.OrgManager;
        var entry = principal.GetOrgRoles().FirstOrDefault(r => r.OrgId == orgId);
        if (entry is null) return null;
        return Enum.TryParse<OrgRole>(entry.Role, true, out var role) ? role : null;
    }

    // ── Space-scoped role checks ──────────────────────────────────────────────

    /// <summary>
    /// Returns true if the user is OrgManager in <paramref name="orgId"/> OR
    /// is assigned as SpaceManager for <paramref name="spaceId"/> (via JWT space_roles claim).
    /// SystemAdmins always pass.
    /// </summary>
    public static bool IsSpaceManagerOrAbove(
        this ClaimsPrincipal principal, Guid orgId, Guid spaceId)
    {
        if (principal.IsSystemAdmin()) return true;
        if (principal.HasOrgRole(orgId, OrgRole.OrgManager)) return true;
        return principal.GetSpaceRoles().Any(s => s.SpaceId == spaceId);
    }

    /// <summary>
    /// Returns true if the user is assigned as SpaceManager for <paramref name="spaceId"/>
    /// (and NOT merely OrgManager). Used to determine effective space role.
    /// </summary>
    public static bool IsAssignedSpaceManager(
        this ClaimsPrincipal principal, Guid spaceId)
        => principal.GetSpaceRoles().Any(s => s.SpaceId == spaceId);
}
