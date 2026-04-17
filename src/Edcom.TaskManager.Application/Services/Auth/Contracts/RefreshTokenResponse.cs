namespace Edcom.TaskManager.Application.Services.Auth.Contracts;

public class RefreshTokenResponse
{
    public string AccessToken  { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
}
