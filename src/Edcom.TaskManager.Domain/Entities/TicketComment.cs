namespace Edcom.TaskManager.Domain.Entities;

public class TicketComment : AuditableModelBase<long>
{
    public long TicketId { get; set; }
    public long AuthorId { get; set; }
    public long? ParentId { get; set; }
    public required string Content { get; set; }
    public bool IsEdited { get; set; } = false;

    [ForeignKey(nameof(TicketId))]
    public Ticket Ticket { get; set; } = null!;

    [ForeignKey(nameof(AuthorId))]
    public User Author { get; set; } = null!;

    [ForeignKey(nameof(ParentId))]
    public TicketComment? Parent { get; set; }

    public ICollection<TicketComment> Replies { get; set; } = [];
}
