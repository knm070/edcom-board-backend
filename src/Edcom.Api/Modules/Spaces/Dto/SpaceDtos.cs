namespace Edcom.Api.Modules.Spaces.Dto;

public record SpaceDto(
    Guid Id,
    Guid OrgId,
    string Name,
    string Type,          // "Internal" | "External"
    string? BoardTemplate,
    string IssueKeyPrefix,
    int IssueCount,
    List<WorkflowStatusDto> Statuses,
    DateTime CreatedAt
);

public record WorkflowStatusDto(
    Guid Id,
    string Name,
    string Color,
    int Position,
    bool IsInitial,
    bool IsTerminal
);

public record UpdateSpaceRequest(string Name, string? BoardTemplate);
