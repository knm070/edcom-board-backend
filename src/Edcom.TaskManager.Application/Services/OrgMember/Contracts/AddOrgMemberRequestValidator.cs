using Edcom.TaskManager.Application.Services.OrgMember.Contracts;

namespace Edcom.TaskManager.Application.Services.OrgMember;

public class AddOrgMemberRequestValidator : AbstractValidator<AddOrgMemberRequest>
{
    public AddOrgMemberRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
    }
}
