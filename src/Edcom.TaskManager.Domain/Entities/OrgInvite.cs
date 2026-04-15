namespace Edcom.TaskManager.Domain.Entities;

public class OrgInvite : AuditableModelBase<long>
{
    public long OrganizationId { get; set; }

    [MaxLength(256)]
    public required string Email { get; set; }

    public required string Token { get; set; }
    public OrgRole Role { get; set; } = OrgRole.Employer;
    public InviteStatus Status { get; set; } = InviteStatus.Pending;
    public DateTime ExpiresAt { get; set; }

    [ForeignKey(nameof(OrganizationId))]
    public Organization Organization { get; set; } = null!;
}
