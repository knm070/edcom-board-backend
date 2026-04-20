namespace Edcom.TaskManager.Application.Services.User.Contracts;

public class UpdateMeRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}
