namespace Edcom.TaskManager.Domain.Entities;

public class Organization : AuditableModelBase<long>
{
    [MaxLength(128)]
    public required string Name { get; set; }

    [MaxLength(64)]
    public required string Slug { get; set; }

    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public long CreatedByUserId { get; set; }

    [ForeignKey(nameof(CreatedByUserId))]
    public User CreatedByUser { get; set; } = null!;

    public ICollection<OrgMember> Members { get; set; } = [];
    public ICollection<Space> Spaces { get; set; } = [];
    public ICollection<OrgInvite> Invites { get; set; } = [];
}
