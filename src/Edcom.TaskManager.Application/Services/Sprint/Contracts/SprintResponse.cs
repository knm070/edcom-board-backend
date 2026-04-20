using Edcom.TaskManager.Domain.Enums;

namespace Edcom.TaskManager.Application.Services.Sprint.Contracts;

public class SprintResponse
{
    public long Id { get; set; }
    public long SpaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Goal { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public SprintStatus Status { get; set; }
    public long CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
