namespace Edcom.Api.Modules.Sprints.Dto;

// ── Requests ─────────────────────────────────────────────────────────────────

public record CreateSprintRequest(
    string Name,
    string? Goal,
    DateTime? StartDate,
    DateTime? EndDate
);

public record UpdateSprintRequest(
    string? Name,
    string? Goal,
    DateTime? StartDate,
    DateTime? EndDate
);

public record CompleteSprintRequest(
    /// <summary>"backlog" moves incomplete issues to the backlog; "next_sprint" moves them to TargetSprintId.</summary>
    string Disposition,
    Guid? TargetSprintId
);

public record SprintIssueRequest(Guid IssueId);

// ── Responses ─────────────────────────────────────────────────────────────────

public record SprintDto(
    Guid Id,
    Guid SpaceId,
    string Name,
    string? Goal,
    DateTime? StartDate,
    DateTime? EndDate,
    string Status,
    int IssueCount,
    int StoryPoints,
    int CompletedStoryPoints,
    DateTime CreatedAt
);

public record SprintVelocityDto(
    Guid SprintId,
    string SprintName,
    int CommittedPoints,
    int CompletedPoints,
    DateTime CompletedAt
);
