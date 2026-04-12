namespace Edcom.Api.Infrastructure.Data.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? PasswordHash { get; set; }
    public string? GoogleId { get; set; }
    public string? MicrosoftId { get; set; }
    public bool IsSystemAdmin { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<OrgMember> OrgMemberships { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<Issue> ReportedIssues { get; set; } = [];
}
