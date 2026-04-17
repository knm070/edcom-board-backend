using Edcom.TaskManager.Api.Controllers.Common;
using Edcom.TaskManager.Application.Services.Auth;
using Edcom.TaskManager.Application.Services.Auth.Contracts;

namespace Edcom.TaskManager.Api.Controllers;

/// <summary>
/// Authentication controller.
/// </summary>
[Route("api/auth")]
public class AuthController(IAuthService authService) : AuthorizedController
{
    /// <summary>
    /// Login with email and password. Returns access and refresh tokens.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IResult> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>
    /// Get the currently authenticated user's profile.
    /// </summary>
    [HttpGet("me")]
    public async Task<IResult> GetMeAsync(CancellationToken cancellationToken = default)
    {
        var result = await authService.GetMeAsync(UserId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>
    /// Exchange a refresh token for a new access and refresh token pair.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IResult> RefreshTokenAsync(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await authService.RefreshTokenAsync(request, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }
}
