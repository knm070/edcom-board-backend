using System.ComponentModel.DataAnnotations;

namespace Edcom.Api.Modules.Identity.Dto;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(2)] string FullName,
    [Required, MinLength(8)] string Password
);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);

public record RefreshRequest(
    [Required] string RefreshToken
);

public record AuthResponse(
    UserDto User,
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    bool IsNewUser = false
);

public record UserDto(
    Guid Id,
    string Email,
    string FullName,
    string? AvatarUrl,
    bool IsSystemAdmin,
    IEnumerable<OrgRoleDto> OrgMemberships
);

public record OrgRoleDto(
    Guid OrgId,
    string OrgName,
    string Role,
    DateTime JoinedAt
);
