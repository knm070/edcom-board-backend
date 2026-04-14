namespace Edcom.Api.Infrastructure.Data.Entities;

/// <summary>
/// Role a user holds within an organisation.
/// Platform-level administrator is represented by <see cref="User.IsSystemAdmin"/>, not this enum.
/// </summary>
public enum OrgRole
{
    OrgManager   = 1,  // formerly OrgTaskManager — full org authority
    SpaceManager = 2,  // NEW — manages one or more specific spaces
    Employer     = 3,  // regular team member
}

public class OrgMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgId { get; set; }
    public Guid UserId { get; set; }
    public OrgRole Role { get; set; } = OrgRole.Employer;
    public Guid? InvitedById { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Organization Organization { get; set; } = null!;
    public User User { get; set; } = null!;
    public User? InvitedBy { get; set; }
}
