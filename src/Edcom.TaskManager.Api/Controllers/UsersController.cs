using Edcom.TaskManager.Api.Controllers.Common;
using Edcom.TaskManager.Application.Services.User;
using Edcom.TaskManager.Application.Services.User.Contracts;

namespace Edcom.TaskManager.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController(IUserService userService) : AuthorizedController
{
    /// <summary>Create a new user account. System admin only.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IResult> AddAsync([FromBody] CreateUserRequest request, CancellationToken ct = default)
    {
        var result = await userService.AddAsync(request, ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Get all users.</summary>
    [HttpGet]
    public async Task<IResult> GetAllAsync(CancellationToken ct = default)
    {
        var result = await userService.GetAllAsync(ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Get user by id.</summary>
    [HttpGet("{id:long}")]
    public async Task<IResult> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var result = await userService.GetByIdAsync(id, ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Update current user profile.</summary>
    [HttpPut("me")]
    public async Task<IResult> UpdateMeAsync([FromBody] UpdateMeRequest request, CancellationToken ct = default)
    {
        var result = await userService.UpdateMeAsync(UserId, request, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Activate or deactivate a user. System admin only.</summary>
    [HttpPatch("{id:long}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IResult> UpdateStatusAsync(long id, [FromBody] UpdateUserStatusRequest request, CancellationToken ct = default)
    {
        var result = await userService.UpdateStatusAsync(UserId, id, request, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Grant or revoke system admin role. System admin only.</summary>
    [HttpPatch("{id:long}/admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IResult> UpdateAdminAsync(long id, [FromBody] UpdateUserAdminRequest request, CancellationToken ct = default)
    {
        var result = await userService.UpdateAdminAsync(UserId, id, request, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }
}
