namespace Edcom.TaskManager.Application.Services.Auth.Contracts;

public class LoginResponse
{
    public string AccessToken   { get; set; } = null!;
    public string RefreshToken  { get; set; } = null!;
    public long   UserId        { get; set; }
    public string Email         { get; set; } = null!;
    public string FullName      { get; set; } = null!;
    public bool   IsSystemAdmin { get; set; }
}
