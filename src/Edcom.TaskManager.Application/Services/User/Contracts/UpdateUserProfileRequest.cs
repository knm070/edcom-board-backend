namespace Edcom.TaskManager.Application.Services.User.Contracts;

public class UpdateUserProfileRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email    { get; set; } = string.Empty;
}
