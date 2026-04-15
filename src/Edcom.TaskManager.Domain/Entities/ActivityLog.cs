namespace Edcom.TaskManager.Domain.Entities;

public class ActivityLog : ModelBase<long>
{
    public long TicketId { get; set; }
    public long ActorId { get; set; }
    public ActivityAction Action { get; set; }

    [MaxLength(64)]
    public string? FieldName { get; set; }

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; }

    [ForeignKey(nameof(TicketId))]
    public Ticket Ticket { get; set; } = null!;

    [ForeignKey(nameof(ActorId))]
    public User Actor { get; set; } = null!;
}
