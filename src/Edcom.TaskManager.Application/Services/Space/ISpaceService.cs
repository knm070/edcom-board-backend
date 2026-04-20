using Edcom.TaskManager.Application.Services.Space.Contracts;

namespace Edcom.TaskManager.Application.Services.Space;

public interface ISpaceService
{
    Task<Result<List<SpaceResponse>>> GetAllByOrgAsync(long orgId, CancellationToken ct);
    Task<Result<SpaceResponse>> GetByIdAsync(long id, CancellationToken ct);
    Task<Result> AddAsync(long orgId, CreateSpaceRequest request, long callerUserId, CancellationToken ct);
    Task<Result> UpdateAsync(long id, UpdateSpaceRequest request, long callerUserId, CancellationToken ct);
    Task<Result> DeleteAsync(long id, long callerUserId, CancellationToken ct);
}
