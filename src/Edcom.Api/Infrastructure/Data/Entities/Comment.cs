namespace Edcom.Api.Infrastructure.Data.Entities;

public class Comment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IssueId { get; set; }
    public Guid? ParentId { get; set; }
    public Guid AuthorId { get; set; }
    public Guid AuthorOrgId { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsEdited { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Issue Issue { get; set; } = null!;
    public Comment? Parent { get; set; }
    public User Author { get; set; } = null!;
    public Organization AuthorOrg { get; set; } = null!;
    public ICollection<Comment> Replies { get; set; } = [];
}
