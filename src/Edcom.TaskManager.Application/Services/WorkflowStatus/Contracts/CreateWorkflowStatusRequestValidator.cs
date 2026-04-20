using Edcom.TaskManager.Application.Services.WorkflowStatus.Contracts;

namespace Edcom.TaskManager.Application.Services.WorkflowStatus;

public class CreateWorkflowStatusRequestValidator : AbstractValidator<CreateWorkflowStatusRequest>
{
    public CreateWorkflowStatusRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Color).NotEmpty().MaximumLength(16);
    }
}
