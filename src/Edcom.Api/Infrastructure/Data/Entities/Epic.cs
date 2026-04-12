namespace Edcom.Api.Infrastructure.Data.Entities;

public class Epic
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SpaceId { get; set; }
    public Guid OrgId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#6366F1";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? StatusId { get; set; }
    public Guid CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Space Space { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public ICollection<Issue> Issues { get; set; } = [];
}
