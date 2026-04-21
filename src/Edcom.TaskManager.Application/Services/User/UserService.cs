using Edcom.TaskManager.Application.Services.User.Contracts;
using Edcom.TaskManager.Infrastructure.Helpers;
using Edcom.TaskManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using UserEntity = Edcom.TaskManager.Domain.Entities.User;

namespace Edcom.TaskManager.Application.Services.User;

public class UserService(AppDbContext dbContext, PasswordHasher passwordHasher) : IUserService
{
    public async Task<Result<UserResponse>> AddAsync(CreateUserRequest request, CancellationToken ct)
    {
        var emailTaken = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == request.Email && !u.IsDeleted, ct);
        if (emailTaken) return UserErrors.EmailAlreadyExists;

        var user = new UserEntity
        {
            FullName      = request.FullName,
            Email         = request.Email,
            PasswordHash  = passwordHasher.Hash(request.Password),
            IsSystemAdmin = request.IsSystemAdmin,
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(ct);

        return new UserResponse
        {
            Id            = user.Id,
            FullName      = user.FullName,
            Email         = user.Email,
            AvatarUrl     = user.AvatarUrl,
            IsSystemAdmin = user.IsSystemAdmin,
            IsActive      = user.IsActive,
            CreatedAt     = user.CreatedAt,
            UpdatedAt     = user.UpdatedAt,
        };
    }

    public async Task<Result<PagedList<UserResponse>>> GetAllAsync(UserFilterRequest filter, CancellationToken ct)
    {
        var query = dbContext.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = $"%{filter.Search}%";
            query = query.Where(u => EF.Functions.ILike(u.FullName, s) || EF.Functions.ILike(u.Email, s));
        }

        if (filter.IsSystemAdmin.HasValue)
            query = query.Where(u => u.IsSystemAdmin == filter.IsSystemAdmin.Value);

        if (filter.IsActive.HasValue)
            query = query.Where(u => u.IsActive == filter.IsActive.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(u => u.FullName)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(u => new UserResponse
            {
                Id            = u.Id,
                FullName      = u.FullName,
                Email         = u.Email,
                AvatarUrl     = u.AvatarUrl,
                IsSystemAdmin = u.IsSystemAdmin,
                IsActive      = u.IsActive,
                CreatedAt     = u.CreatedAt,
                UpdatedAt     = u.UpdatedAt,
            })
            .ToListAsync(ct);

        return new PagedList<UserResponse>(items, filter.Page, filter.PageSize, total);
    }

    public async Task<Result<UserResponse>> GetByIdAsync(long id, CancellationToken ct)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == id && !u.IsDeleted)
            .Select(u => new UserResponse
            {
                Id            = u.Id,
                FullName      = u.FullName,
                Email         = u.Email,
                AvatarUrl     = u.AvatarUrl,
                IsSystemAdmin = u.IsSystemAdmin,
                IsActive      = u.IsActive,
                CreatedAt     = u.CreatedAt,
                UpdatedAt     = u.UpdatedAt,
            })
            .SingleOrDefaultAsync(ct);

        return user is null ? UserErrors.NotFound : user;
    }

    public async Task<Result> UpdateMeAsync(long callerUserId, UpdateMeRequest request, CancellationToken ct)
    {
        var user = await dbContext.Users
            .SingleOrDefaultAsync(u => u.Id == callerUserId && !u.IsDeleted, ct);
        if (user is null) return UserErrors.NotFound;

        user.FullName  = request.FullName;
        user.AvatarUrl = request.AvatarUrl;

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UpdateStatusAsync(long callerUserId, long targetUserId, UpdateUserStatusRequest request, CancellationToken ct)
    {
        if (!request.IsActive && callerUserId == targetUserId)
            return UserErrors.CannotDeactivateSelf;

        var user = await dbContext.Users
            .SingleOrDefaultAsync(u => u.Id == targetUserId && !u.IsDeleted, ct);
        if (user is null) return UserErrors.NotFound;

        user.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UpdateAdminAsync(long callerUserId, long targetUserId, UpdateUserAdminRequest request, CancellationToken ct)
    {
        if (!request.IsSystemAdmin && callerUserId == targetUserId)
            return UserErrors.CannotRemoveOwnAdmin;

        var user = await dbContext.Users
            .SingleOrDefaultAsync(u => u.Id == targetUserId && !u.IsDeleted, ct);
        if (user is null) return UserErrors.NotFound;

        user.IsSystemAdmin = request.IsSystemAdmin;
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }
}
