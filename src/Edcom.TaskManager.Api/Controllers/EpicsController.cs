using Edcom.TaskManager.Api.Controllers.Common;
using Edcom.TaskManager.Application.Services.Epic;
using Edcom.TaskManager.Application.Services.Epic.Contracts;

namespace Edcom.TaskManager.Api.Controllers;

[ApiController]
[Route("api/spaces/{spaceId:long}/epics")]
public class EpicsController(IEpicService epicService) : AuthorizedController
{
    /// <summary>Get all epics for a space with progress rollup.</summary>
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

    /// <summary>Delete epic (soft delete). Fails if epic has active tickets. Requires OrgManager role.</summary>
    [HttpDelete("{id:long}")]
    public async Task<IResult> DeleteAsync(long spaceId, long id, CancellationToken ct = default)
    {
        var result = await epicService.DeleteAsync(id, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Get detailed progress for an epic (ticket counts + story point rollup).</summary>
    [HttpGet("{id:long}/progress")]
    public async Task<IResult> GetProgressAsync(long spaceId, long id, CancellationToken ct = default)
    {
        var result = await epicService.GetProgressAsync(id, ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Transition epic status. Requires OrgManager role.</summary>
    [HttpPatch("{id:long}/status")]
    public async Task<IResult> UpdateStatusAsync(long spaceId, long id, [FromBody] UpdateEpicStatusRequest request, CancellationToken ct = default)
    {
        var result = await epicService.UpdateStatusAsync(id, request, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Reorder epics within a space. Requires OrgManager role.</summary>
    [HttpPost("reorder")]
    public async Task<IResult> ReorderAsync(long spaceId, [FromBody] ReorderEpicsRequest request, CancellationToken ct = default)
    {
        var result = await epicService.ReorderAsync(spaceId, request, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Assign an existing ticket to this epic. Requires OrgManager role.</summary>
    [HttpPost("{id:long}/tickets/{ticketId:long}")]
    public async Task<IResult> AssignTicketAsync(long spaceId, long id, long ticketId, CancellationToken ct = default)
    {
        var result = await epicService.AssignTicketAsync(id, ticketId, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Detach a ticket from this epic (sets EpicId = null). Requires OrgManager role.</summary>
    [HttpDelete("{id:long}/tickets/{ticketId:long}")]
    public async Task<IResult> DetachTicketAsync(long spaceId, long id, long ticketId, CancellationToken ct = default)
    {
        var result = await epicService.DetachTicketAsync(id, ticketId, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }
}
