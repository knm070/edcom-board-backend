namespace Edcom.Api.Modules.Authorization.Claims;

/// <summary>Canonical JWT claim names used across the Edcom platform.</summary>
public static class EdcomClaimTypes
{
    public const string UserId     = "sub";
    public const string Email      = "email";
    public const string FullName   = "full_name";
    /// <summary>Boolean string — "true" | "false".</summary>
    public const string IsAdmin    = "is_admin";
    /// <summary>JSON-serialised array of <see cref="OrgRoleClaim"/>.</summary>
    public const string OrgRoles   = "org_roles";
    /// <summary>
    /// JSON-serialised array of <see cref="SpaceRoleClaim"/>.
    /// Only contains entries where the user IS assigned as SpaceManager.
    /// </summary>
    public const string SpaceRoles = "space_roles";
}
