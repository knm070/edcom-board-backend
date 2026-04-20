namespace Edcom.TaskManager.Application.Services.Auth.Contracts;

public class MeResponse
{
    public long      Id             { get; set; }
    public string    Email          { get; set; } = null!;
    public string    FullName       { get; set; } = null!;
    public string?   AvatarUrl      { get; set; }
    public bool      IsSystemAdmin  { get; set; }
    public bool      IsActive       { get; set; }
    public DateTime  CreatedAt      { get; set; }
    public List<OrgMembershipDto> OrgMemberships { get; set; } = [];
}
