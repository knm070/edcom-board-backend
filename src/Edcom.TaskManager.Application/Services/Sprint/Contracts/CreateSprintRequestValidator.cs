using Edcom.TaskManager.Application.Services.Sprint.Contracts;

namespace Edcom.TaskManager.Application.Services.Sprint;

public class CreateSprintRequestValidator : AbstractValidator<CreateSprintRequest>
{
    public CreateSprintRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
    }
}
