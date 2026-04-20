namespace Edcom.TaskManager.Application.Services.Tag.Contracts;

public class CreateTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#888888";
}
