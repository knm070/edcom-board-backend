namespace Edcom.TaskManager.Domain.Entities;

public class Epic : AuditableModelBase<long>
{
    public long SpaceId { get; set; }

    [MaxLength(256)]
    public required string Title { get; set; }

    public string? Description { get; set; }

    [MaxLength(16)]
    public string Color { get; set; } = "#7F77DD";

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public long CreatedByUserId { get; set; }

    [ForeignKey(nameof(SpaceId))]
    public Space Space { get; set; } = null!;

    [ForeignKey(nameof(CreatedByUserId))]
    public User CreatedByUser { get; set; } = null!;

    public ICollection<Ticket> Tickets { get; set; } = [];
}
