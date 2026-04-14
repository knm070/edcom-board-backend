namespace Edcom.Api.Modules.Epics.Dto;

// ── Requests ─────────────────────────────────────────────────────────────────

public record CreateEpicRequest(
    string Title,
    string? Description,
    string? Color,
    DateTime? StartDate,
    DateTime? EndDate
);

public record UpdateEpicRequest(
    string? Title,
    string? Description,
    string? Color,
    DateTime? StartDate,
    DateTime? EndDate
);

// ── Response ─────────────────────────────────────────────────────────────────

public record EpicDto(
    Guid Id,
    Guid SpaceId,
    Guid OrgId,
    string Title,
    string? Description,
    string Color,
    DateTime? StartDate,
    DateTime? EndDate,
    int IssueCount,
    int CompletedIssueCount,
    int StoryPoints,
    int CompletedStoryPoints,
    DateTime CreatedAt
);
