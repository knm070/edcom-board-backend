using Edcom.TaskManager.Application.Services.Auth.Contracts;

namespace Edcom.TaskManager.Application.Services.Auth;

public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<Result<MeResponse>> GetMeAsync(long userId, CancellationToken cancellationToken);

    Task<Result<RefreshTokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken);
}
