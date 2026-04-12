namespace Edcom.Api.Infrastructure.Data.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid OrgId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
}

public class ActivityLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IssueId { get; set; }
    public Guid OrgId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Issue Issue { get; set; } = null!;
    public User Actor { get; set; } = null!;
}
