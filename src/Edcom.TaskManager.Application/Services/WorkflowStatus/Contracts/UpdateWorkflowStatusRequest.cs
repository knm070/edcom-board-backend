using Edcom.TaskManager.Domain.Enums;

namespace Edcom.TaskManager.Application.Services.WorkflowStatus.Contracts;

public class UpdateWorkflowStatusRequest
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#888888";
    public decimal Position { get; set; }
    public WorkflowStatusBaseType BaseType { get; set; }
}
