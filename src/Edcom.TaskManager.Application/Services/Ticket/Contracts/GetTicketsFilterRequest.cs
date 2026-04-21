namespace Edcom.TaskManager.Application.Services.Ticket.Contracts;

public class GetTicketsFilterRequest
{
    public string? Search { get; set; }
    public List<long>? AssigneeIds { get; set; }
    public List<int>? Priorities { get; set; }
    public List<int>? Types { get; set; }
    public List<long>? EpicIds { get; set; }
    public long? SprintId { get; set; }
    public bool? Backlog { get; set; }
}
