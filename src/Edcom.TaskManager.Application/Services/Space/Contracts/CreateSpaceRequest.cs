using Edcom.TaskManager.Domain.Enums;

namespace Edcom.TaskManager.Application.Services.Space.Contracts;

public class CreateSpaceRequest
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public BoardType BoardType { get; set; }
    public string IssueKeyPrefix { get; set; } = string.Empty;
}
