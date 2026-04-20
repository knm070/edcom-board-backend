namespace Edcom.TaskManager.Application.Services.Space.Contracts;

public class UpdateSpaceRequest
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
