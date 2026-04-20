using Edcom.TaskManager.Application.Services.Epic.Contracts;

namespace Edcom.TaskManager.Application.Services.Epic;

public interface IEpicService
{
    Task<Result<List<EpicResponse>>> GetAllBySpaceAsync(long spaceId, CancellationToken ct);
    Task<Result<EpicResponse>> GetByIdAsync(long id, CancellationToken ct);
    Task<Result> AddAsync(long spaceId, CreateEpicRequest request, long callerUserId, CancellationToken ct);
    Task<Result> UpdateAsync(long id, UpdateEpicRequest request, long callerUserId, CancellationToken ct);
    Task<Result> DeleteAsync(long id, long callerUserId, CancellationToken ct);
}
