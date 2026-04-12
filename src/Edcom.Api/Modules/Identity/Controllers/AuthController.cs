using System.Security.Claims;
using Edcom.Api.Modules.Identity.Dto;
using Edcom.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edcom.Api.Modules.Identity.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest req, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(req, ct);
        return Ok(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest req, CancellationToken ct)
    {
        var result = await auth.LoginAsync(req, ct);
        return Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody] RefreshRequest req, CancellationToken ct)
    {
        var result = await auth.RefreshAsync(req.RefreshToken, ct);
        return Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req, CancellationToken ct)
    {
        await auth.RevokeAsync(req.RefreshToken, ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")!);
        var result = await auth.GetMeAsync(userId, ct);
        return Ok(result);
    }
}
