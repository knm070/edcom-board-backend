namespace Edcom.Api.Modules.CrossOrgTickets.Dto;

// ── Requests ─────────────────────────────────────────────────────────────────

public record SendCrossOrgTicketRequest(
    string Title,
    string? Description,
    string Priority,          // "High" | "Low"
    Guid TargetOrgId,
    List<FileAttachmentDto>? Attachments
);

public record FileAttachmentDto(string FileName, string Url, long Size);

public record AssignTicketRequest(
    Guid? AssigneeId,
    int? EstimationHours,
    DateTime? DueDate
);

public record ProgressTicketRequest(
    string TargetStatusName,   // "To Do" | "In Progress" | "In Review" | "Done"
    string? CompletionNotes    // required when TargetStatusName == "Done"
);

public record RejectTicketRequest(string Comment);

// ── Responses ────────────────────────────────────────────────────────────────

public record CrossOrgTicketDto(
    Guid Id,
    Guid CreatorOrgId,
    string CreatorOrgName,
    Guid ReceiverOrgId,
    string ReceiverOrgName,
    Guid ExternalIssueId,
    Guid? InternalMirrorIssueId,
    string ExternalStatus,
    string Title,
    string Priority,
    string? Description,
    List<FileAttachmentDto> Attachments,
    Guid? AssigneeId,
    string? AssigneeName,
    int? EstimationHours,
    string? CompletionNotes,
    bool IsCreatorOrgDeleted,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
