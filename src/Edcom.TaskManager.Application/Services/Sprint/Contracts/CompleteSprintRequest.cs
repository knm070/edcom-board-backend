namespace Edcom.TaskManager.Application.Services.Sprint.Contracts;

public class CompleteSprintRequest
{
    public string Disposition { get; set; } = "backlog";
    public long? TargetSprintId { get; set; }
}
