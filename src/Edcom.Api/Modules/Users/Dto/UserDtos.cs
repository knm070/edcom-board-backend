namespace Edcom.Api.Modules.Users.Dto;

// ── Responses ─────────────────────────────────────────────────────────────────

public record AdminUserDto(
    Guid     Id,
    string   Email,
    string   FullName,
    string?  AvatarUrl,
    bool     IsSystemAdmin,
    bool     IsActive,
    int      OrgCount,
    DateTime CreatedAt
);

public record CreateUserResponse(
    AdminUserDto User,
    string       TempPassword   // plain-text; returned once, never stored
);

// ── Requests ──────────────────────────────────────────────────────────────────

public record CreateUserRequest(
    string Email,
    string FullName,
    string Password,
    bool   IsSystemAdmin
);

public record UpdateUserRequest(
    string? FullName,
    string? Email,
    string? AvatarUrl,
    bool?   IsActive,
    bool?   IsSystemAdmin
);
