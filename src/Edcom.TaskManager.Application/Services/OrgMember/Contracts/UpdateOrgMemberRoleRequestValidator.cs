using Edcom.TaskManager.Application.Services.OrgMember.Contracts;

namespace Edcom.TaskManager.Application.Services.OrgMember;

public class UpdateOrgMemberRoleRequestValidator : AbstractValidator<UpdateOrgMemberRoleRequest>
{
    public UpdateOrgMemberRoleRequestValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
    }
}
