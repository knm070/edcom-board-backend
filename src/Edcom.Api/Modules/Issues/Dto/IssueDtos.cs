namespace Edcom.Api.Modules.Issues.Dto;

// ── Requests ────────────────────────────────────────────────────────────────
public record CreateIssueRequest(
    string Title,
    string? Description,
    string Type,        // Task | Bug | Story | Subtask
    string Priority,    // Low | Medium | High | Critical
    Guid StatusId,
    Guid? SprintId,
    Guid? EpicId,
    List<Guid>? AssigneeIds,
    int? StoryPoints,
    DateTime? DueDate
);

public record UpdateIssueRequest(
    string? Title,
    string? Description,
    string? Type,
    string? Priority,
    Guid? StatusId,
    Guid? SprintId,
    Guid? EpicId,
    int? StoryPoints,
    DateTime? DueDate
);

public record MoveIssueRequest(Guid StatusId);

public record AssignIssueRequest(List<Guid> AssigneeIds);

public record ReorderIssuesRequest(List<Guid> OrderedIds);

public record BulkUpdateIssueRequest(
    List<Guid> IssueIds,
    Guid? SprintId,
    bool ClearSprint,
    string? Priority,
    Guid? EpicId,
    bool ClearEpic,
    bool Delete
);

// ── Responses ────────────────────────────────────────────────────────────────
public record IssueListDto(
    Guid Id,
    string Key,          // e.g. "PROJ-42"
    string Title,
    string Type,
    string Priority,
    string Status,
    string StatusColor,
    Guid StatusId,
    List<AssigneeDto> Assignees,
    Guid? SprintId,
    Guid? EpicId,
    int? StoryPoints,
    DateTime? DueDate,
    DateTime CreatedAt
);

public record IssueDetailDto(
    Guid Id,
    string Key,
    string Title,
    string? Description,
    string Type,
    string Priority,
    string Status,
    string StatusColor,
    Guid StatusId,
    List<AssigneeDto> Assignees,
    AssigneeDto Reporter,
    Guid? SprintId,
    Guid? EpicId,
    int? StoryPoints,
    DateTime? DueDate,
    List<CommentDto> Comments,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record AssigneeDto(Guid Id, string FullName, string? AvatarUrl);

public record CommentDto(
    Guid Id,
    Guid AuthorId,
    string AuthorName,
    string? AuthorAvatar,
    string Body,
    Guid? ParentId,
    DateTime CreatedAt
);

public record WorklogDto(
    Guid Id,
    Guid IssueId,
    Guid UserId,
    string UserName,
    string? UserAvatar,
    decimal Hours,
    string? Description,
    DateTime Date,
    DateTime CreatedAt
);

public record ActivityLogDto(
    Guid Id,
    Guid IssueId,
    Guid ActorId,
    string ActorName,
    string? ActorAvatar,
    string Action,
    string? Metadata,
    DateTime CreatedAt
);

public record UpdateWorklogRequest(
    decimal? Hours,
    string? Description,
    DateTime? Date
);
