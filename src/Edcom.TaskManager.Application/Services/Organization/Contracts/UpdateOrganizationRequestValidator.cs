namespace Edcom.TaskManager.Application.Services.Organization.Contracts;

public class UpdateOrganizationRequestValidator : AbstractValidator<UpdateOrganizationRequest>
{
    public UpdateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(128).WithMessage("Name must not exceed 128 characters.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug is required.")
            .MaximumLength(64).WithMessage("Slug must not exceed 64 characters.")
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase alphanumeric with hyphens (e.g. 'my-org').");

        RuleFor(x => x.LogoUrl)
            .MaximumLength(2048)
            .When(x => !string.IsNullOrEmpty(x.LogoUrl));
    }
}
