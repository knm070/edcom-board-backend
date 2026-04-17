namespace Edcom.TaskManager.Application.Services.Auth;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials = Error.Validation("Auth.InvalidCredentials", "Invalid email or password.");
    public static readonly Error InvalidRefreshToken = Error.Validation("Auth.InvalidRefreshToken", "Refresh token is invalid or expired.");
    public static readonly Error UserInactive        = Error.Validation("Auth.UserInactive", "User account is inactive.");
}
