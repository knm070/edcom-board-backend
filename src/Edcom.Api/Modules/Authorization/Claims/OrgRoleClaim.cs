using System.Text.Json.Serialization;

namespace Edcom.Api.Modules.Authorization.Claims;

/// <summary>
/// One entry inside the <c>org_roles</c> JWT claim.
/// Each user can have different roles in different organisations simultaneously.
/// </summary>
public sealed record OrgRoleClaim(
    [property: JsonPropertyName("orgId")]   Guid   OrgId,
    [property: JsonPropertyName("orgName")] string OrgName,
    [property: JsonPropertyName("role")]    string Role     // "OrgTaskManager" | "Employer" | "Viewer"
);
