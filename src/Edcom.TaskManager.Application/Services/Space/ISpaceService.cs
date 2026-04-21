using Edcom.TaskManager.Application.Services.Space.Contracts;
using Edcom.TaskManager.Domain.Abstractions;

namespace Edcom.TaskManager.Application.Services.Space;

public interface ISpaceService
{
    Task<Result<PagedList<SpaceResponse>>> GetAllByOrgAsync(long orgId, long callerUserId, SpaceFilterRequest filter, CancellationToken ct);
    Task<Result<SpaceResponse>> GetByIdAsync(long id, long callerUserId, CancellationToken ct);
    Task<Result> AddAsync(long orgId, CreateSpaceRequest request, long callerUserId, CancellationToken ct);
    Task<Result> UpdateAsync(long id, UpdateSpaceRequest request, long callerUserId, CancellationToken ct);
    Task<Result> DeleteAsync(long id, long callerUserId, CancellationToken ct);
}
