namespace Edcom.Api.Infrastructure.Data.Entities;

public enum BoardType { Kanban, Scrum }

public class Space
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Immutable after creation.</summary>
    public BoardType BoardType { get; set; }
    public string IssueKeyPrefix { get; set; } = string.Empty;
    public int IssueCounter { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Organization Organization { get; set; } = null!;
    public ICollection<WorkflowStatus> WorkflowStatuses { get; set; } = [];
    public ICollection<WorkflowTransition> WorkflowTransitions { get; set; } = [];
    public ICollection<SpaceAssignment> SpaceAssignments { get; set; } = [];
    public ICollection<Sprint> Sprints { get; set; } = [];
    public ICollection<Epic> Epics { get; set; } = [];
    public ICollection<Issue> Issues { get; set; } = [];
}
