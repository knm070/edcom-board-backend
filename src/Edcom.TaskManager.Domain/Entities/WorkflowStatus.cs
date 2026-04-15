using System.ComponentModel.DataAnnotations.Schema;

namespace Edcom.TaskManager.Domain.Entities;

public class WorkflowStatus : AuditableModelBase<long>
{
    public long SpaceId { get; set; }

    [MaxLength(64)]
    public required string Name { get; set; }

    [MaxLength(16)]
    public string Color { get; set; } = "#888888";

    [Column(TypeName = "decimal(10,4)")]
    public decimal Position { get; set; }

    public WorkflowStatusBaseType BaseType { get; set; } = WorkflowStatusBaseType.Custom;

    [ForeignKey(nameof(SpaceId))]
    public Space Space { get; set; } = null!;

    public ICollection<Ticket> Tickets { get; set; } = [];
}
