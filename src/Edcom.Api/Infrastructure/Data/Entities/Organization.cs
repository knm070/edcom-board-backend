namespace Edcom.Api.Infrastructure.Data.Entities;

public enum OrgStatus { Pending, Active, Rejected }

public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public Guid CreatedById { get; set; }
    public OrgStatus Status { get; set; } = OrgStatus.Pending;
    public string? RejectionReason { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User CreatedBy { get; set; } = null!;
    public ICollection<OrgMember> Members { get; set; } = [];
    public ICollection<Space> Spaces { get; set; } = [];
}
