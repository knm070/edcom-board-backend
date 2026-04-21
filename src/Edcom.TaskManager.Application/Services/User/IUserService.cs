using Edcom.TaskManager.Application.Services.User.Contracts;
using Edcom.TaskManager.Domain.Abstractions;

namespace Edcom.TaskManager.Application.Services.User;

public interface IUserService
{
    Task<Result<PagedList<UserResponse>>> GetAllAsync(UserFilterRequest filter, CancellationToken ct);
    Task<Result<UserResponse>> GetByIdAsync(long id, CancellationToken ct);
    Task<Result<UserResponse>> AddAsync(CreateUserRequest request, CancellationToken ct);
    Task<Result> UpdateMeAsync(long callerUserId, UpdateMeRequest request, CancellationToken ct);
    Task<Result> UpdateStatusAsync(long callerUserId, long targetUserId, UpdateUserStatusRequest request, CancellationToken ct);
    Task<Result> UpdateAdminAsync(long callerUserId, long targetUserId, UpdateUserAdminRequest request, CancellationToken ct);
}
