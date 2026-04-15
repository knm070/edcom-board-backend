namespace Edcom.TaskManager.Domain.Entities;

public class User : AuditableModelBase<long>
{
    [MaxLength(128)]
    public required string FullName { get; set; }

    [MaxLength(256)]
    public required string Email { get; set; }

    public string? PasswordHash { get; set; }
    public string? GoogleId { get; set; }
    public string? MicrosoftId { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsSystemAdmin { get; set; } = false;
    public bool IsActive { get; set; } = true;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<OrgMember> OrgMemberships { get; set; } = [];
    public ICollection<Ticket> AssignedTickets { get; set; } = [];
    public ICollection<Ticket> ReportedTickets { get; set; } = [];
    public ICollection<TicketComment> Comments { get; set; } = [];
    public ICollection<ActivityLog> Activities { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
}
