using Edcom.TaskManager.Domain.Enums;

namespace Edcom.TaskManager.Application.Services.Space.Contracts;

public class SpaceResponse
{
    public long Id { get; set; }
    public long OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public BoardType BoardType { get; set; }
    public string IssueKeyPrefix { get; set; } = string.Empty;
    public int IssueCounter { get; set; }
    public int IssueCount   { get; set; }
    public bool IsActive { get; set; }
    public long CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
