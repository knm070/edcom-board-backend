using Edcom.TaskManager.Application.Services.Space.Contracts;

namespace Edcom.TaskManager.Application.Services.Space;

public class CreateSpaceRequestValidator : AbstractValidator<CreateSpaceRequest>
{
    public CreateSpaceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Slug).MaximumLength(64).When(x => !string.IsNullOrWhiteSpace(x.Slug));
        RuleFor(x => x.IssueKeyPrefix).MaximumLength(8).When(x => !string.IsNullOrWhiteSpace(x.IssueKeyPrefix));
    }
}
