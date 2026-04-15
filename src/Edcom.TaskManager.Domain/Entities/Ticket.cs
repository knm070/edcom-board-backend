using System.ComponentModel.DataAnnotations.Schema;

namespace Edcom.TaskManager.Domain.Entities;

public class Ticket : AuditableModelBase<long>
{
    public long SpaceId { get; set; }
    public long OrganizationId { get; set; }

    [MaxLength(16)]
    public required string KeyNumber { get; set; }

    public TicketType Type { get; set; }
    public Priority Priority { get; set; } = Priority.Medium;

    [MaxLength(512)]
    public required string Title { get; set; }

    public string? Description { get; set; }
    public long? StatusId { get; set; }
    public long? SprintId { get; set; }
    public long? EpicId { get; set; }
    public long ReporterId { get; set; }
    public long? AssigneeId { get; set; }
    public DateTime? DueDate { get; set; }

    [Column(TypeName = "decimal(6,2)")]
    public decimal? EstimationHours { get; set; }

    public int BacklogOrder { get; set; }
    public int? StoryPoints { get; set; }

    [ForeignKey(nameof(SpaceId))]
    public Space Space { get; set; } = null!;

    [ForeignKey(nameof(OrganizationId))]
    public Organization Organization { get; set; } = null!;

    [ForeignKey(nameof(StatusId))]
    public WorkflowStatus? Status { get; set; }

    [ForeignKey(nameof(SprintId))]
    public Sprint? Sprint { get; set; }

    [ForeignKey(nameof(EpicId))]
    public Epic? Epic { get; set; }

    [ForeignKey(nameof(ReporterId))]
    public User Reporter { get; set; } = null!;

    [ForeignKey(nameof(AssigneeId))]
    public User? Assignee { get; set; }

    public ICollection<TicketTag> TicketTags { get; set; } = [];
    public ICollection<TicketComment> Comments { get; set; } = [];
    public ICollection<ActivityLog> ActivityLogs { get; set; } = [];
}
