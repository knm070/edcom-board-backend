using Edcom.TaskManager.Application.Services.Space.Contracts;

namespace Edcom.TaskManager.Application.Services.Space;

public class UpdateSpaceRequestValidator : AbstractValidator<UpdateSpaceRequest>
{
    public UpdateSpaceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
    }
}
