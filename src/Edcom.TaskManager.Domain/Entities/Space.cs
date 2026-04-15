namespace Edcom.TaskManager.Domain.Entities;

public class Space : AuditableModelBase<long>
{
    public long OrganizationId { get; set; }

    [MaxLength(128)]
    public required string Name { get; set; }

    [MaxLength(64)]
    public required string Slug { get; set; }

    public BoardType BoardType { get; set; }

    [MaxLength(8)]
    public required string IssueKeyPrefix { get; set; }

    public int IssueCounter { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public long CreatedByUserId { get; set; }

    [ForeignKey(nameof(OrganizationId))]
    public Organization Organization { get; set; } = null!;

    [ForeignKey(nameof(CreatedByUserId))]
    public User CreatedByUser { get; set; } = null!;

    public ICollection<WorkflowStatus> WorkflowStatuses { get; set; } = [];
    public ICollection<Tag> Tags { get; set; } = [];
    public ICollection<Sprint> Sprints { get; set; } = [];
    public ICollection<Epic> Epics { get; set; } = [];
    public ICollection<Ticket> Tickets { get; set; } = [];
}
