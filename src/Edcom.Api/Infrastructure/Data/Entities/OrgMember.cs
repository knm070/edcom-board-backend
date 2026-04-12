namespace Edcom.Api.Infrastructure.Data.Entities;

public enum MemberRole { OrgTaskManager, Employer, Viewer }

public class OrgMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgId { get; set; }
    public Guid UserId { get; set; }
    public MemberRole Role { get; set; } = MemberRole.Employer;
    public Guid? InvitedById { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Organization Organization { get; set; } = null!;
    public User User { get; set; } = null!;
    public User? InvitedBy { get; set; }
}
