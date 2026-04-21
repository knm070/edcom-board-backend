namespace Edcom.TaskManager.Application.Services.Ticket.Contracts;

public class ActivityLogResponse
{
    public long Id { get; set; }
    public long ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string? ActorAvatarUrl { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; }
}
