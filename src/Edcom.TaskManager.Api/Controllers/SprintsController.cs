using Edcom.TaskManager.Api.Controllers.Common;
using Edcom.TaskManager.Application.Services.Sprint;
using Edcom.TaskManager.Application.Services.Sprint.Contracts;

namespace Edcom.TaskManager.Api.Controllers;

[ApiController]
[Route("api/spaces/{spaceId:long}/sprints")]
public class SprintsController(ISprintService sprintService) : AuthorizedController
{
    /// <summary>Get all sprints for a space with pagination and search.</summary>
    [HttpGet]
    public async Task<IResult> GetAllAsync(long spaceId, [FromQuery] SprintFilterRequest filter, CancellationToken ct = default)
    {
        var result = await sprintService.GetAllBySpaceAsync(spaceId, filter, ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Get sprint by id.</summary>
    [HttpGet("{id:long}")]
    public async Task<IResult> GetByIdAsync(long spaceId, long id, CancellationToken ct = default)
    {
        var result = await sprintService.GetByIdAsync(id, ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Create a sprint. Requires OrgManager role.</summary>
    [HttpPost]
    public async Task<IResult> AddAsync(long spaceId, [FromBody] CreateSprintRequest request, CancellationToken ct = default)
    {
        var result = await sprintService.AddAsync(spaceId, request, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Update sprint. Requires OrgManager role.</summary>
    [HttpPut("{id:long}")]
    public async Task<IResult> UpdateAsync(long spaceId, long id, [FromBody] UpdateSprintRequest request, CancellationToken ct = default)
    {
        var result = await sprintService.UpdateAsync(id, request, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Start a sprint. Moves all backlog-status tickets in the sprint to the initial workflow status. Requires OrgManager role.</summary>
    [HttpPost("{id:long}/start")]
    public async Task<IResult> StartAsync(long spaceId, long id, CancellationToken ct = default)
    {
        var result = await sprintService.StartAsync(id, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Complete a sprint. Moves incomplete tickets to backlog or a target planned sprint. Requires OrgManager role.</summary>
    [HttpPost("{id:long}/complete")]
    public async Task<IResult> CompleteAsync(long spaceId, long id, [FromBody] CompleteSprintRequest request, CancellationToken ct = default)
    {
        var result = await sprintService.CompleteAsync(id, request, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Delete sprint (soft delete). Requires OrgManager role.</summary>
    [HttpDelete("{id:long}")]
    public async Task<IResult> DeleteAsync(long spaceId, long id, CancellationToken ct = default)
    {
        var result = await sprintService.DeleteAsync(id, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }
}
