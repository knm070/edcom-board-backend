namespace Edcom.TaskManager.Application.Services.User.Contracts;

public class CreateUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsSystemAdmin { get; set; } = false;
}
