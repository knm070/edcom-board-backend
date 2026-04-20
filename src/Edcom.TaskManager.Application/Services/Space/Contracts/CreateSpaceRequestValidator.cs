using Edcom.TaskManager.Application.Services.Space.Contracts;

namespace Edcom.TaskManager.Application.Services.Space;

public class CreateSpaceRequestValidator : AbstractValidator<CreateSpaceRequest>
{
    public CreateSpaceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(64);
        RuleFor(x => x.IssueKeyPrefix).NotEmpty().MaximumLength(8);
    }
}
