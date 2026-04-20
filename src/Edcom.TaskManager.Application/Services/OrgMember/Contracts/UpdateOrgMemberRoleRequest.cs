using Edcom.TaskManager.Domain.Enums;

namespace Edcom.TaskManager.Application.Services.OrgMember.Contracts;

public class UpdateOrgMemberRoleRequest
{
    public OrgRole Role { get; set; }
}
