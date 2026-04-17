using System.Security.Cryptography;
using Edcom.TaskManager.Application.Services.Auth.Contracts;
using Edcom.TaskManager.Domain.Entities;
using Edcom.TaskManager.Infrastructure.Authentication;
using Edcom.TaskManager.Infrastructure.Helpers;
using Edcom.TaskManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edcom.TaskManager.Application.Services.Auth;

public class AuthService(
    AppDbContext dbContext,
    PasswordHasher passwordHasher,
    IJwtProvider jwtProvider) : IAuthService
{
    private const int RefreshTokenExpiryDays = 7;

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted, cancellationToken);

        if (user is null || user.PasswordHash is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
            return AuthErrors.InvalidCredentials;

        if (!user.IsActive)
            return AuthErrors.UserInactive;

        var (accessToken, refreshToken) = await IssueTokensAsync(user.Id, user.Email, user.IsSystemAdmin, cancellationToken);

        return new LoginResponse
        {
            AccessToken   = accessToken,
            RefreshToken  = refreshToken,
            UserId        = user.Id,
            Email         = user.Email,
            FullName      = user.FullName,
            IsSystemAdmin = user.IsSystemAdmin,
        };
    }

    public async Task<Result<MeResponse>> GetMeAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && !u.IsDeleted)
            .Select(u => new MeResponse
            {
                Id            = u.Id,
                Email         = u.Email,
                FullName      = u.FullName,
                AvatarUrl     = u.AvatarUrl,
                IsSystemAdmin = u.IsSystemAdmin,
                IsActive      = u.IsActive,
                CreatedAt     = u.CreatedAt,
            })
            .SingleOrDefaultAsync(cancellationToken);

        return user is null ? AuthErrors.InvalidCredentials : user;
    }

    public async Task<Result<RefreshTokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = HashRefreshToken(request.RefreshToken);

        var stored = await dbContext.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(
                t => t.TokenHash == tokenHash && !t.IsRevoked && !t.IsDeleted,
                cancellationToken);

        if (stored is null || stored.ExpiresAt <= DateTime.UtcNow)
            return AuthErrors.InvalidRefreshToken;

        if (!stored.User.IsActive)
            return AuthErrors.UserInactive;

        stored.IsRevoked = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        var (accessToken, newRefreshToken) = await IssueTokensAsync(
            stored.User.Id,
            stored.User.Email,
            stored.User.IsSystemAdmin,
            cancellationToken);

        return new RefreshTokenResponse
        {
            AccessToken  = accessToken,
            RefreshToken = newRefreshToken,
        };
    }

    private async Task<(string AccessToken, string RefreshToken)> IssueTokensAsync(
        long userId,
        string email,
        bool isSystemAdmin,
        CancellationToken cancellationToken)
    {
        var role = isSystemAdmin ? "Admin" : "User";

        var accessToken = jwtProvider.Generate(userId, email, role);

        var plainRefreshToken = GeneratePlainRefreshToken();
        var tokenHash         = HashRefreshToken(plainRefreshToken);

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId    = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays),
            CreatedAt = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return (accessToken, plainRefreshToken);
    }

    private static string GeneratePlainRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    private static string HashRefreshToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash  = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
