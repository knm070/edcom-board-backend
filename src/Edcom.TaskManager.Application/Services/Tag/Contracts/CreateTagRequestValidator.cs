using Edcom.TaskManager.Application.Services.Tag.Contracts;

namespace Edcom.TaskManager.Application.Services.Tag;

public class CreateTagRequestValidator : AbstractValidator<CreateTagRequest>
{
    public CreateTagRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Color).NotEmpty().MaximumLength(16);
    }
}
