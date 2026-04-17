namespace Edcom.TaskManager.Application.Services.Auth.Contracts;

public class RefreshTokenRequest
{
    public required string RefreshToken { get; set; }
}
