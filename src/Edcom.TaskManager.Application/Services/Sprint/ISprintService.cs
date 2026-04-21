using Edcom.TaskManager.Application.Services.Sprint.Contracts;
using Edcom.TaskManager.Domain.Abstractions;

namespace Edcom.TaskManager.Application.Services.Sprint;

public interface ISprintService
{
    Task<Result<PagedList<SprintResponse>>> GetAllBySpaceAsync(long spaceId, SprintFilterRequest filter, CancellationToken ct);
    Task<Result<SprintResponse>> GetByIdAsync(long id, CancellationToken ct);
    Task<Result> AddAsync(long spaceId, CreateSprintRequest request, long callerUserId, CancellationToken ct);
    Task<Result> UpdateAsync(long id, UpdateSprintRequest request, long callerUserId, CancellationToken ct);
    Task<Result> DeleteAsync(long id, long callerUserId, CancellationToken ct);
}
