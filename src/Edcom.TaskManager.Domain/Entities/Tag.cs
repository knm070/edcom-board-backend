namespace Edcom.TaskManager.Domain.Entities;

public class Tag : AuditableModelBase<long>
{
    public long SpaceId { get; set; }

    [MaxLength(64)]
    public required string Name { get; set; }

    [MaxLength(16)]
    public string Color { get; set; } = "#888888";

    [ForeignKey(nameof(SpaceId))]
    public Space Space { get; set; } = null!;

    public ICollection<TicketTag> TicketTags { get; set; } = [];
}
