using Edcom.TaskManager.Application.Services.Epic.Contracts;

namespace Edcom.TaskManager.Application.Services.Epic;

public class CreateEpicRequestValidator : AbstractValidator<CreateEpicRequest>
{
    public CreateEpicRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Color).NotEmpty().MaximumLength(16);
    }
}
