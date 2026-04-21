using Edcom.TaskManager.Application.Services.WorkflowStatus.Contracts;

namespace Edcom.TaskManager.Application.Services.WorkflowStatus;

public interface IWorkflowStatusService
{
    Task<Result<List<WorkflowStatusResponse>>> GetAllBySpaceAsync(long spaceId, CancellationToken ct);
    Task<Result<WorkflowStatusResponse>> GetByIdAsync(long id, CancellationToken ct);
    Task<Result> AddAsync(long spaceId, CreateWorkflowStatusRequest request, long callerUserId, CancellationToken ct);
    Task<Result> UpdateAsync(long id, UpdateWorkflowStatusRequest request, long callerUserId, CancellationToken ct);
    Task<Result> DeleteAsync(long id, long callerUserId, CancellationToken ct);
    Task<Result> ReorderAsync(long spaceId, List<long> orderedIds, long callerUserId, CancellationToken ct);
}
