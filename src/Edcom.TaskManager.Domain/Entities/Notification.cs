namespace Edcom.TaskManager.Domain.Entities;

public class Notification : ModelBase<long>
{
    public long UserId { get; set; }
    public long OrganizationId { get; set; }
    public NotificationType Type { get; set; }
    public required string Message { get; set; }
    public long? ReferenceId { get; set; }

    [MaxLength(32)]
    public string? ReferenceType { get; set; }

    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(OrganizationId))]
    public Organization Organization { get; set; } = null!;
}
