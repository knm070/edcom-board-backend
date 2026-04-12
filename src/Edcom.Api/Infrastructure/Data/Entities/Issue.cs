namespace Edcom.Api.Infrastructure.Data.Entities;

public enum IssuePriority { Low, Medium, High, Critical }
public enum IssueType { Task, Bug, Story, Subtask }

public class Issue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SpaceId { get; set; }
    public Guid OrgId { get; set; }
    public int KeyNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IssueType Type { get; set; } = IssueType.Task;
    public IssuePriority Priority { get; set; } = IssuePriority.Medium;
    public Guid StatusId { get; set; }
    public Guid? SprintId { get; set; }
    public Guid? EpicId { get; set; }
    public Guid ReporterId { get; set; }
    public int? StoryPoints { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Space Space { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
    public WorkflowStatus Status { get; set; } = null!;
    public Sprint? Sprint { get; set; }
    public Epic? Epic { get; set; }
    public User Reporter { get; set; } = null!;
    public ICollection<IssueAssignee> Assignees { get; set; } = [];
    public ICollection<Comment> Comments { get; set; } = [];
    public ICollection<ActivityLog> ActivityLogs { get; set; } = [];
    public CrossOrgTicket? CrossOrgTicketAsExternal { get; set; }
    public CrossOrgTicket? CrossOrgTicketAsMirror { get; set; }
}

public class IssueAssignee
{
    public Guid IssueId { get; set; }
    public Guid UserId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public Guid? AssignedById { get; set; }

    public Issue Issue { get; set; } = null!;
    public User User { get; set; } = null!;
}
