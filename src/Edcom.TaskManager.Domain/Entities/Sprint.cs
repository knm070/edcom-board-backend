namespace Edcom.TaskManager.Domain.Entities;

public class Sprint : AuditableModelBase<long>
{
    public long SpaceId { get; set; }

    [MaxLength(128)]
    public required string Name { get; set; }

    public string? Goal { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public SprintStatus Status { get; set; } = SprintStatus.Planning;
    public long CreatedByUserId { get; set; }

    [ForeignKey(nameof(SpaceId))]
    public Space Space { get; set; } = null!;

    [ForeignKey(nameof(CreatedByUserId))]
    public User CreatedByUser { get; set; } = null!;

    public ICollection<Ticket> Tickets { get; set; } = [];
}
