using Edcom.TaskManager.Application.Services.Tag.Contracts;

namespace Edcom.TaskManager.Application.Services.Tag;

public interface ITagService
{
    Task<Result<List<TagResponse>>> GetAllBySpaceAsync(long spaceId, CancellationToken ct);
    Task<Result<TagResponse>> GetByIdAsync(long id, CancellationToken ct);
    Task<Result> AddAsync(long spaceId, CreateTagRequest request, long callerUserId, CancellationToken ct);
    Task<Result> UpdateAsync(long id, UpdateTagRequest request, long callerUserId, CancellationToken ct);
    Task<Result> DeleteAsync(long id, long callerUserId, CancellationToken ct);
}
