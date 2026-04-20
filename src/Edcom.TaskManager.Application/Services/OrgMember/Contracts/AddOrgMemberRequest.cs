using Edcom.TaskManager.Domain.Enums;

namespace Edcom.TaskManager.Application.Services.OrgMember.Contracts;

public class AddOrgMemberRequest
{
    public long UserId { get; set; }
    public OrgRole Role { get; set; } = OrgRole.Employer;
}
