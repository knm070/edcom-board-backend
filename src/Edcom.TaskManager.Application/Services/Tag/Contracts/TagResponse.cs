namespace Edcom.TaskManager.Application.Services.Tag.Contracts;

public class TagResponse
{
    public long Id { get; set; }
    public long SpaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
