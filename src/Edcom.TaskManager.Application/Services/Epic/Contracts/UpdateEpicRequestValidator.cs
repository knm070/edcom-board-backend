using Edcom.TaskManager.Application.Services.Epic.Contracts;

namespace Edcom.TaskManager.Application.Services.Epic;

public class UpdateEpicRequestValidator : AbstractValidator<UpdateEpicRequest>
{
    public UpdateEpicRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Color).NotEmpty().MaximumLength(16);
    }
}
