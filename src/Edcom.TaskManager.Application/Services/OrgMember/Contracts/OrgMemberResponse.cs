using Edcom.TaskManager.Domain.Enums;

namespace Edcom.TaskManager.Application.Services.OrgMember.Contracts;

public class OrgMemberResponse
{
    public long Id { get; set; }
    public long OrganizationId { get; set; }
    public long UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string? UserAvatarUrl { get; set; }
    public OrgRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
