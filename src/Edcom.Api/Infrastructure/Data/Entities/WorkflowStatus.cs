namespace Edcom.Api.Infrastructure.Data.Entities;

public class WorkflowStatus
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>null = system external workflow shared across all orgs</summary>
    public Guid? SpaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#6B7280";
    public int Position { get; set; }
    public bool IsInitial { get; set; }
    public bool IsTerminal { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Space? Space { get; set; }
    public ICollection<Issue> Issues { get; set; } = [];
    public ICollection<WorkflowTransition> TransitionsFrom { get; set; } = [];
    public ICollection<WorkflowTransition> TransitionsTo { get; set; } = [];
}

public class WorkflowTransition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? SpaceId { get; set; }
    public Guid FromStatusId { get; set; }
    public Guid ToStatusId { get; set; }
    /// <summary>
    /// JSON array of role strings that may use this transition,
    /// e.g. ["OrgTaskManager","Employer"]. Null means all roles allowed.
    /// OrgTaskManagers always bypass this check on internal spaces.
    /// </summary>
    public string? AllowedRolesJson { get; set; }

    public Space? Space { get; set; }
    public WorkflowStatus FromStatus { get; set; } = null!;
    public WorkflowStatus ToStatus { get; set; } = null!;
}
