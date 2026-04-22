using Edcom.TaskManager.Domain.Enums;

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
    public EpicStatus Status { get; set; }
    public long? OwnerId { get; set; }
    public long RankOrder { get; set; }
    public long CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Progress rollup
    public int TotalTickets { get; set; }
    public int DoneTickets { get; set; }
    public int InProgressTickets { get; set; }
    public int ToDoTickets { get; set; }
    public int PercentComplete { get; set; }
    public int TotalStoryPoints { get; set; }
    public int CompletedStoryPoints { get; set; }
    public int PercentCompleteByPoints { get; set; }
}
