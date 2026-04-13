namespace Edcom.Api.Modules.Spaces.Dto;

// ── Space responses ──────────────────────────────────────────────────────────

public record SpaceDto(
    Guid Id,
    Guid OrgId,
    string Name,
    string Type,           // "Internal" | "External"
    string? BoardTemplate,
    string Status,         // "Active" | "PendingTemplateSelection"
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

public record WorkflowTransitionDto(
    Guid Id,
    Guid FromStatusId,
    string FromStatusName,
    Guid ToStatusId,
    string ToStatusName,
    List<string> AllowedRoles   // empty = all roles
);

// ── Space requests ────────────────────────────────────────────────────────────

public record UpdateSpaceRequest(string Name, string? BoardTemplate);

public record SetInternalTemplateRequest(string BoardTemplate); // "Kanban" | "Scrum"

// ── Workflow management requests ──────────────────────────────────────────────

public record AddStatusRequest(
    string Name,
    string Color,
    int Position           // insert at this position; existing statuses shift right
);

public record UpdateStatusRequest(string? Name, string? Color);

public record ReorderStatusesRequest(List<Guid> OrderedStatusIds);

public record AddTransitionRequest(
    Guid FromStatusId,
    Guid ToStatusId,
    List<string>? AllowedRoles  // null = all roles; ["OrgTaskManager","Employer"]
);
