namespace Edcom.Api.Infrastructure.Data.Entities;

public class WorkflowStatus
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Every status belongs to exactly one space — no global/null workflow.</summary>
    public Guid SpaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#6B7280";
    public int Position { get; set; }
    public bool IsInitial { get; set; }
    /// <summary>Marks a status as "done" — Employers cannot transition to this status.</summary>
    public bool IsDoneStatus { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Space Space { get; set; } = null!;
    public ICollection<Issue> Issues { get; set; } = [];
    public ICollection<WorkflowTransition> TransitionsFrom { get; set; } = [];
    public ICollection<WorkflowTransition> TransitionsTo { get; set; } = [];
}

public class WorkflowTransition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Every transition belongs to exactly one space — no global/null workflow.</summary>
    public Guid SpaceId { get; set; }
    public Guid FromStatusId { get; set; }
    public Guid ToStatusId { get; set; }
    /// <summary>
    /// JSON array of role strings that may use this transition,
    /// e.g. ["OrgManager","SpaceManager","Employer"]. Null means all roles allowed.
    /// OrgManager always bypasses this check entirely.
    /// </summary>
    public string? AllowedRolesJson { get; set; }

    public Space Space { get; set; } = null!;
    public WorkflowStatus FromStatus { get; set; } = null!;
    public WorkflowStatus ToStatus { get; set; } = null!;
}
