namespace Edcom.TaskManager.Application.Services.User.Contracts;

public class UserOrgResponse
{
    public long   MembershipId { get; set; }
    public long   OrgId        { get; set; }
    public string OrgName      { get; set; } = string.Empty;
    public string Role         { get; set; } = string.Empty;
    public DateTime JoinedAt   { get; set; }
}
