using Edcom.TaskManager.Domain.Enums;

namespace Edcom.TaskManager.Application.Services.Ticket.Contracts;

public class TicketResponse
{
    public long Id { get; set; }
    public long SpaceId { get; set; }
    public long OrganizationId { get; set; }
    public string KeyNumber { get; set; } = string.Empty;
    public TicketType Type { get; set; }
    public Priority Priority { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long? StatusId { get; set; }
    public string? StatusName { get; set; }
    public string? StatusColor { get; set; }
    public int? StatusBaseType { get; set; }
    public long? SprintId { get; set; }
    public string? SprintName { get; set; }
    public long? EpicId { get; set; }
    public string? EpicTitle { get; set; }
    public long ReporterId { get; set; }
    public string ReporterName { get; set; } = string.Empty;
    public long? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? EstimationHours { get; set; }
    public int BacklogOrder { get; set; }
    public int? StoryPoints { get; set; }
    public List<TagSummary> Tags { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class TagSummary
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
}
