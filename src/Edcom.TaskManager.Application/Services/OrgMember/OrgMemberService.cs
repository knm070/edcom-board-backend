using Edcom.TaskManager.Application.Services.OrgMember.Contracts;
using Edcom.TaskManager.Domain.Enums;
using Edcom.TaskManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OrgMemberEntity = Edcom.TaskManager.Domain.Entities.OrgMember;

namespace Edcom.TaskManager.Application.Services.OrgMember;

public class OrgMemberService(AppDbContext dbContext) : IOrgMemberService
{
    public async Task<Result<PagedList<OrgMemberResponse>>> GetAllByOrgAsync(long orgId, long callerUserId, OrgMemberFilterRequest filter, CancellationToken ct)
    {
        if (!await IsMemberAsync(orgId, callerUserId, ct)) return OrgMemberErrors.Forbidden;

        var query = dbContext.OrgMembers
            .AsNoTracking()
            .Where(m => m.OrganizationId == orgId && !m.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = $"%{filter.Search}%";
            query = query.Where(m => EF.Functions.ILike(m.User.FullName, s) || EF.Functions.ILike(m.User.Email, s));
        }

        if (filter.Role.HasValue)
            query = query.Where(m => m.Role == filter.Role.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(m => m.User.FullName)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(m => new OrgMemberResponse
            {
                Id             = m.Id,
                OrganizationId = m.OrganizationId,
                UserId         = m.UserId,
                UserFullName   = m.User.FullName,
                UserEmail      = m.User.Email,
                UserAvatarUrl  = m.User.AvatarUrl,
                Role           = m.Role,
                CreatedAt      = m.CreatedAt,
                UpdatedAt      = m.UpdatedAt,
            })
            .ToListAsync(ct);

        return new PagedList<OrgMemberResponse>(items, filter.Page, filter.PageSize, total);
    }

    public async Task<Result<OrgMemberResponse>> GetByIdAsync(long id, long callerUserId, CancellationToken ct)
    {
        var member = await dbContext.OrgMembers
            .AsNoTracking()
            .Where(m => m.Id == id && !m.IsDeleted)
            .Select(m => new OrgMemberResponse
            {
                Id             = m.Id,
                OrganizationId = m.OrganizationId,
                UserId         = m.UserId,
                UserFullName   = m.User.FullName,
                UserEmail      = m.User.Email,
                UserAvatarUrl  = m.User.AvatarUrl,
                Role           = m.Role,
                CreatedAt      = m.CreatedAt,
                UpdatedAt      = m.UpdatedAt,
            })
            .SingleOrDefaultAsync(ct);

        if (member is null) return OrgMemberErrors.NotFound;
        if (!await IsMemberAsync(member.OrganizationId, callerUserId, ct)) return OrgMemberErrors.Forbidden;
        return member;
    }

    private async Task<bool> IsMemberAsync(long orgId, long callerUserId, CancellationToken ct)
    {
        var isAdmin = await dbContext.Users.AsNoTracking()
            .AnyAsync(u => u.Id == callerUserId && u.IsSystemAdmin && !u.IsDeleted, ct);
        if (isAdmin) return true;

        return await dbContext.OrgMembers.AsNoTracking()
            .AnyAsync(m => m.OrganizationId == orgId && m.UserId == callerUserId && !m.IsDeleted, ct);
    }

    private async Task<bool> IsAuthorizedAsync(long orgId, long callerUserId, CancellationToken ct)
    {
        var isAdmin = await dbContext.Users.AsNoTracking()
            .AnyAsync(u => u.Id == callerUserId && u.IsSystemAdmin && !u.IsDeleted, ct);
        if (isAdmin) return true;

        return await dbContext.OrgMembers.AsNoTracking()
            .AnyAsync(m => m.OrganizationId == orgId && m.UserId == callerUserId
                        && m.Role == OrgRole.OrgManager && !m.IsDeleted, ct);
    }

    public async Task<Result> AddAsync(long orgId, AddOrgMemberRequest request, long callerUserId, CancellationToken ct)
    {
        if (!await IsAuthorizedAsync(orgId, callerUserId, ct)) return OrgMemberErrors.Forbidden;

        var existing = await dbContext.OrgMembers
            .SingleOrDefaultAsync(m => m.OrganizationId == orgId && m.UserId == request.UserId, ct);

        if (existing is not null)
        {
            if (!existing.IsDeleted) return OrgMemberErrors.AlreadyMember;
            existing.IsDeleted = false;
            existing.Role      = request.Role;
            await dbContext.SaveChangesAsync(ct);
            return Result.Success();
        }

        dbContext.OrgMembers.Add(new OrgMemberEntity
        {
            OrganizationId = orgId,
            UserId         = request.UserId,
            Role           = request.Role,
        });

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UpdateRoleAsync(long id, UpdateOrgMemberRoleRequest request, long callerUserId, CancellationToken ct)
    {
        var member = await dbContext.OrgMembers
            .SingleOrDefaultAsync(m => m.Id == id && !m.IsDeleted, ct);
        if (member is null) return OrgMemberErrors.NotFound;

        if (!await IsAuthorizedAsync(member.OrganizationId, callerUserId, ct)) return OrgMemberErrors.Forbidden;

        member.Role = request.Role;
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(long id, long callerUserId, CancellationToken ct)
    {
        var member = await dbContext.OrgMembers
            .SingleOrDefaultAsync(m => m.Id == id && !m.IsDeleted, ct);
        if (member is null) return OrgMemberErrors.NotFound;

        if (!await IsAuthorizedAsync(member.OrganizationId, callerUserId, ct)) return OrgMemberErrors.Forbidden;

        member.IsDeleted = true;
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }
}
