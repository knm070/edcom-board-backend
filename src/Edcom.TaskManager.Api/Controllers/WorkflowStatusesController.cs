using Edcom.TaskManager.Api.Controllers.Common;
using Edcom.TaskManager.Application.Services.WorkflowStatus;
using Edcom.TaskManager.Application.Services.WorkflowStatus.Contracts;

namespace Edcom.TaskManager.Api.Controllers;

[ApiController]
[Route("api/spaces/{spaceId:long}/workflow-statuses")]
public class WorkflowStatusesController(IWorkflowStatusService workflowStatusService) : AuthorizedController
{
    /// <summary>Get all workflow statuses for a space.</summary>
    [HttpGet]
    public async Task<IResult> GetAllAsync(long spaceId, CancellationToken ct = default)
    {
        var result = await workflowStatusService.GetAllBySpaceAsync(spaceId, ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Get workflow status by id.</summary>
    [HttpGet("{id:long}")]
    public async Task<IResult> GetByIdAsync(long spaceId, long id, CancellationToken ct = default)
    {
        var result = await workflowStatusService.GetByIdAsync(id, ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Create workflow status. Requires OrgManager role.</summary>
    [HttpPost]
    public async Task<IResult> AddAsync(long spaceId, [FromBody] CreateWorkflowStatusRequest request, CancellationToken ct = default)
    {
        var result = await workflowStatusService.AddAsync(spaceId, request, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Update workflow status. Requires OrgManager role.</summary>
    [HttpPut("{id:long}")]
    public async Task<IResult> UpdateAsync(long spaceId, long id, [FromBody] UpdateWorkflowStatusRequest request, CancellationToken ct = default)
    {
        var result = await workflowStatusService.UpdateAsync(id, request, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Delete workflow status (soft delete). Requires OrgManager role.</summary>
    [HttpDelete("{id:long}")]
    public async Task<IResult> DeleteAsync(long spaceId, long id, CancellationToken ct = default)
    {
        var result = await workflowStatusService.DeleteAsync(id, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Reorder workflow statuses. Requires OrgManager role.</summary>
    [HttpPut("reorder")]
    public async Task<IResult> ReorderAsync(long spaceId, [FromBody] ReorderWorkflowStatusesRequest request, CancellationToken ct = default)
    {
        var result = await workflowStatusService.ReorderAsync(spaceId, request.OrderedIds, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }
}
