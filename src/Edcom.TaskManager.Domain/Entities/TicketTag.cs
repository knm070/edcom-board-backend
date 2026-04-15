namespace Edcom.TaskManager.Domain.Entities;

public class TicketTag
{
    public long TicketId { get; set; }
    public long TagId { get; set; }

    [ForeignKey(nameof(TicketId))]
    public Ticket Ticket { get; set; } = null!;

    [ForeignKey(nameof(TagId))]
    public Tag Tag { get; set; } = null!;
}
