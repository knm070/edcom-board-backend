using Edcom.TaskManager.Application.Services.Sprint.Contracts;

namespace Edcom.TaskManager.Application.Services.Sprint;

public class UpdateSprintRequestValidator : AbstractValidator<UpdateSprintRequest>
{
    public UpdateSprintRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
    }
}
