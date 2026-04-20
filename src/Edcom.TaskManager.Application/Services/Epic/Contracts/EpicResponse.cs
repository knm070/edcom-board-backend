namespace Edcom.TaskManager.Application.Services.Epic.Contracts;

public class EpicResponse
{
    public long Id { get; set; }
    public long SpaceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public long CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
