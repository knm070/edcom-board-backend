namespace Edcom.TaskManager.Application.Services.WorkflowStatus.Contracts;

public class ReorderWorkflowStatusesRequest
{
    public List<long> OrderedIds { get; set; } = new();
}
