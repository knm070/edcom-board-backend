using Edcom.TaskManager.Application.Services.WorkflowStatus.Contracts;

namespace Edcom.TaskManager.Application.Services.WorkflowStatus;

public class UpdateWorkflowStatusRequestValidator : AbstractValidator<UpdateWorkflowStatusRequest>
{
    public UpdateWorkflowStatusRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Color).NotEmpty().MaximumLength(16);
    }
}
