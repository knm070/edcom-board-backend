namespace Edcom.Api.Infrastructure.Data.Entities;

public enum SprintStatus { Planned, Active, Completed }

public class Sprint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SpaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Goal { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public SprintStatus Status { get; set; } = SprintStatus.Planned;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Space Space { get; set; } = null!;
    public ICollection<Issue> Issues { get; set; } = [];
}
