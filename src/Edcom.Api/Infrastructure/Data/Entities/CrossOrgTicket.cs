namespace Edcom.Api.Infrastructure.Data.Entities;

public class CrossOrgTicket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CreatorOrgId { get; set; }
    public Guid ReceiverOrgId { get; set; }
    /// <summary>The ticket living in the receiver's External Space.</summary>
    public Guid ExternalIssueId { get; set; }
    /// <summary>The mirror tracking ticket in the creator's Internal Space.</summary>
    public Guid? InternalMirrorIssueId { get; set; }
    public Guid CreatedById { get; set; }
    /// <summary>Notes added by the receiver OrgTaskManager when marking Done.</summary>
    public string? CompletionNotes { get; set; }
    /// <summary>True when the creator org was soft-deleted mid-lifecycle; mirror creation is skipped.</summary>
    public bool IsCreatorOrgDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Organization CreatorOrg { get; set; } = null!;
    public Organization ReceiverOrg { get; set; } = null!;
    public Issue ExternalIssue { get; set; } = null!;
    public Issue? InternalMirrorIssue { get; set; }
    public User CreatedBy { get; set; } = null!;
}
