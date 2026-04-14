namespace Edcom.Api.Modules.Organizations.Dto;

// ── Requests ────────────────────────────────────────────────────────────────
public record CreateOrgRequest(string Name, string? LogoUrl);

public record RejectOrgRequest(string? Reason);

public record UpdateOrgRequest(string Name, string? LogoUrl);

public record InviteMemberRequest(string Email, string Role); // "OrgManager" | "SpaceManager" | "Employer"

public record UpdateMemberRoleRequest(string Role);

// ── Responses ────────────────────────────────────────────────────────────────
public record OrgDto(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    string MyRole,
    int MemberCount,
    int SpaceCount,
    DateTime CreatedAt
);

public record OrgPendingDto(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    Guid RequestedById,
    string RequestedByName,
    string RequestedByEmail,
    DateTime RequestedAt
);

public record OrgMemberDto(
    Guid UserId,
    string FullName,
    string Email,
    string? AvatarUrl,
    string Role,
    DateTime JoinedAt
);
