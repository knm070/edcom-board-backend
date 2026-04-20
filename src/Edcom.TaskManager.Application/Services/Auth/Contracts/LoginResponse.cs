namespace Edcom.TaskManager.Application.Services.Auth.Contracts;

public class LoginResponse
{
    public string AccessToken   { get; set; } = null!;
    public string RefreshToken  { get; set; } = null!;
    public long   UserId        { get; set; }
    public string Email         { get; set; } = null!;
    public string FullName      { get; set; } = null!;
    public string? AvatarUrl    { get; set; }
    public bool   IsSystemAdmin { get; set; }
    public List<OrgMembershipDto> OrgMemberships { get; set; } = [];
}

public class OrgMembershipDto
{
    public long   OrgId   { get; set; }
    public string OrgName { get; set; } = null!;
    public int    Role    { get; set; }
    public DateTime JoinedAt { get; set; }
}
