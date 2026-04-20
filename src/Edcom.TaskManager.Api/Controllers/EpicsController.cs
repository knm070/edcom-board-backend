using Edcom.TaskManager.Api.Controllers.Common;
using Edcom.TaskManager.Application.Services.Epic;
using Edcom.TaskManager.Application.Services.Epic.Contracts;

namespace Edcom.TaskManager.Api.Controllers;

[ApiController]
[Route("api/spaces/{spaceId:long}/epics")]
public class EpicsController(IEpicService epicService) : AuthorizedController
{
    /// <summary>Get all epics for a space.</summary>
    [HttpGet]
    public async Task<IResult> GetAllAsync(long spaceId, CancellationToken ct = default)
    {
        var result = await epicService.GetAllBySpaceAsync(spaceId, ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Get epic by id.</summary>
    [HttpGet("{id:long}")]
    public async Task<IResult> GetByIdAsync(long spaceId, long id, CancellationToken ct = default)
    {
        var result = await epicService.GetByIdAsync(id, ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Create an epic. Requires OrgManager role.</summary>
    [HttpPost]
    public async Task<IResult> AddAsync(long spaceId, [FromBody] CreateEpicRequest request, CancellationToken ct = default)
    {
        var result = await epicService.AddAsync(spaceId, request, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Update epic. Requires OrgManager role.</summary>
    [HttpPut("{id:long}")]
    public async Task<IResult> UpdateAsync(long spaceId, long id, [FromBody] UpdateEpicRequest request, CancellationToken ct = default)
    {
        var result = await epicService.UpdateAsync(id, request, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Delete epic (soft delete). Requires OrgManager role.</summary>
    [HttpDelete("{id:long}")]
    public async Task<IResult> DeleteAsync(long spaceId, long id, CancellationToken ct = default)
    {
        var result = await epicService.DeleteAsync(id, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }
}
