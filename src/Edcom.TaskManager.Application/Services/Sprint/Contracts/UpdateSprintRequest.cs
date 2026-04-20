using Edcom.TaskManager.Domain.Enums;

namespace Edcom.TaskManager.Application.Services.Sprint.Contracts;

public class UpdateSprintRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Goal { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public SprintStatus Status { get; set; }
}
