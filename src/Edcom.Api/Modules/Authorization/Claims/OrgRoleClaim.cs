using System.Text.Json.Serialization;

namespace Edcom.Api.Modules.Authorization.Claims;

/// <summary>
/// One entry inside the <c>org_roles</c> JWT claim.
/// Each user can have different roles in different organisations simultaneously.
/// </summary>
public sealed record OrgRoleClaim(
    [property: JsonPropertyName("orgId")]   Guid   OrgId,
    [property: JsonPropertyName("orgSlug")] string OrgSlug,
    [property: JsonPropertyName("role")]    string Role     // "OrgManager" | "SpaceManager" | "Employer"
);

/// <summary>
/// One entry inside the <c>space_roles</c> JWT claim.
/// Only present for users who are explicitly assigned as SpaceManager for a specific space.
/// </summary>
public sealed record SpaceRoleClaim(
    [property: JsonPropertyName("orgId")]      Guid   OrgId,
    [property: JsonPropertyName("spaceId")]    Guid   SpaceId,
    [property: JsonPropertyName("spaceSlug")]  string SpaceSlug
);
