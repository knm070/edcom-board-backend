using Edcom.TaskManager.Domain.Enums;

namespace Edcom.TaskManager.Application.Services.Ticket.Contracts;

public class CreateTicketRequest
{
    public TicketType Type { get; set; } = TicketType.Task;
    public Priority Priority { get; set; } = Priority.Medium;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long? StatusId { get; set; }
    public long? SprintId { get; set; }
    public long? EpicId { get; set; }
    public long? AssigneeId { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? EstimationHours { get; set; }
    public int? StoryPoints { get; set; }
    public List<long> TagIds { get; set; } = [];
}
