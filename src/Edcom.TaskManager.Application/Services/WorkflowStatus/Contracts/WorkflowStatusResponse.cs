using Edcom.TaskManager.Domain.Enums;

namespace Edcom.TaskManager.Application.Services.WorkflowStatus.Contracts;

public class WorkflowStatusResponse
{
    public long Id { get; set; }
    public long SpaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public decimal Position { get; set; }
    public WorkflowStatusBaseType BaseType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
