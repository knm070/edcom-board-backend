using System.Security.Claims;
using System.Text.Json;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Authorization.Claims;

namespace Edcom.Api.Modules.Authorization.Extensions;

public static class ClaimsPrincipalExtensions
{
    // ── Identity ─────────────────────────────────────────────────────────────

    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(EdcomClaimTypes.UserId)
               ?? principal.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
               ?? throw new UnauthorizedAccessException("User ID claim not present.");
        return Guid.Parse(raw);
    }

    public static bool IsSystemAdmin(this ClaimsPrincipal principal)
        => string.Equals(
            principal.FindFirstValue(EdcomClaimTypes.IsAdmin),
            "true",
            StringComparison.OrdinalIgnoreCase);

    // ── Org-role extraction ───────────────────────────────────────────────────

    private static readonly JsonSerializerOptions _jsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<OrgRoleClaim> GetOrgRoles(this ClaimsPrincipal principal)
    {
        var json = principal.FindFirstValue(EdcomClaimTypes.OrgRoles);
        if (string.IsNullOrWhiteSpace(json)) return [];
        return JsonSerializer.Deserialize<List<OrgRoleClaim>>(json, _jsonOpts) ?? [];
    }

    // ── Org-scoped role checks ────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the user is a System Admin OR holds at least one of the
    /// specified <paramref name="roles"/> in the given <paramref name="orgId"/>.
    /// </summary>
    public static bool HasOrgRole(
        this ClaimsPrincipal principal, Guid orgId, params MemberRole[] roles)
    {
        if (principal.IsSystemAdmin()) return true;
        var orgRoles = principal.GetOrgRoles();
        return orgRoles.Any(r =>
            r.OrgId == orgId &&
            roles.Any(allowed => string.Equals(allowed.ToString(), r.Role, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Returns true if the user holds the given role in ANY org (or is Admin).</summary>
    public static bool HasAnyOrgRole(this ClaimsPrincipal principal, params MemberRole[] roles)
    {
        if (principal.IsSystemAdmin()) return true;
        var orgRoles = principal.GetOrgRoles();
        return orgRoles.Any(r =>
            roles.Any(allowed => string.Equals(allowed.ToString(), r.Role, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Returns true if the user is a member (any role) of the given org, or is Admin.</summary>
    public static bool IsMemberOfOrg(this ClaimsPrincipal principal, Guid orgId)
    {
        if (principal.IsSystemAdmin()) return true;
        return principal.GetOrgRoles().Any(r => r.OrgId == orgId);
    }

    /// <summary>
    /// Returns the <see cref="MemberRole"/> the user holds in <paramref name="orgId"/>,
    /// or <c>null</c> if they are not a member.
    /// System Admins are treated as <see cref="MemberRole.OrgTaskManager"/>.
    /// </summary>
    public static MemberRole? GetOrgRole(this ClaimsPrincipal principal, Guid orgId)
    {
        if (principal.IsSystemAdmin()) return MemberRole.OrgTaskManager;
        var entry = principal.GetOrgRoles().FirstOrDefault(r => r.OrgId == orgId);
        if (entry is null) return null;
        return Enum.TryParse<MemberRole>(entry.Role, true, out var role) ? role : null;
    }
}
