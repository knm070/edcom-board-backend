using Edcom.TaskManager.Application.Services.User.Contracts;

namespace Edcom.TaskManager.Application.Services.User;

public class UpdateMeRequestValidator : AbstractValidator<UpdateMeRequest>
{
    public UpdateMeRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.AvatarUrl).MaximumLength(512).When(x => x.AvatarUrl is not null);
    }
}
