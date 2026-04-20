namespace Edcom.TaskManager.Application.Services.Sprint.Contracts;

public class CreateSprintRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Goal { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
