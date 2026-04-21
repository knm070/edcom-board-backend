using Edcom.TaskManager.Api.Controllers.Common;
using Edcom.TaskManager.Application.Services.Space;
using Edcom.TaskManager.Application.Services.Space.Contracts;

namespace Edcom.TaskManager.Api.Controllers;

[ApiController]
[Route("api/organizations/{orgId:long}/spaces")]
public class SpacesController(ISpaceService spaceService) : AuthorizedController
{
    /// <summary>Get all spaces in an organization with pagination and search.</summary>
    [HttpGet]
    public async Task<IResult> GetAllAsync(long orgId, [FromQuery] SpaceFilterRequest filter, CancellationToken ct = default)
    {
        var result = await spaceService.GetAllByOrgAsync(orgId, UserId, filter, ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Get space by id.</summary>
    [HttpGet("{id:long}")]
    public async Task<IResult> GetByIdAsync(long orgId, long id, CancellationToken ct = default)
    {
        var result = await spaceService.GetByIdAsync(id, UserId, ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Create a new space. Requires OrgManager role.</summary>
    [HttpPost]
    public async Task<IResult> AddAsync(long orgId, [FromBody] CreateSpaceRequest request, CancellationToken ct = default)
    {
        var result = await spaceService.AddAsync(orgId, request, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Update space. Requires OrgManager role.</summary>
    [HttpPut("{id:long}")]
    public async Task<IResult> UpdateAsync(long orgId, long id, [FromBody] UpdateSpaceRequest request, CancellationToken ct = default)
    {
        var result = await spaceService.UpdateAsync(id, request, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Delete space (soft delete). Requires OrgManager role.</summary>
    [HttpDelete("{id:long}")]
    public async Task<IResult> DeleteAsync(long orgId, long id, CancellationToken ct = default)
    {
        var result = await spaceService.DeleteAsync(id, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }
}
