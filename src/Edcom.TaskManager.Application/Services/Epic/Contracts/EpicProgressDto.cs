namespace Edcom.TaskManager.Application.Services.Epic.Contracts;

public class EpicProgressDto
{
    public long EpicId { get; set; }
    public int TotalTickets { get; set; }
    public int DoneTickets { get; set; }
    public int InProgressTickets { get; set; }
    public int ToDoTickets { get; set; }
    public int PercentComplete { get; set; }
    public int TotalStoryPoints { get; set; }
    public int CompletedStoryPoints { get; set; }
    public int PercentCompleteByPoints { get; set; }
}
