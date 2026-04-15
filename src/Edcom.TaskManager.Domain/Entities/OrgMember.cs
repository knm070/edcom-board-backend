namespace Edcom.TaskManager.Domain.Entities;

public class OrgMember : AuditableModelBase<long>
{
    public long OrganizationId { get; set; }
    public long UserId { get; set; }
    public OrgRole Role { get; set; }

    [ForeignKey(nameof(OrganizationId))]
    public Organization Organization { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
