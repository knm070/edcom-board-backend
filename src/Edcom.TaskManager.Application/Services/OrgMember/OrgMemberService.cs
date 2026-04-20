using Edcom.TaskManager.Application.Services.OrgMember.Contracts;
using Edcom.TaskManager.Domain.Enums;
using Edcom.TaskManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OrgMemberEntity = Edcom.TaskManager.Domain.Entities.OrgMember;

namespace Edcom.TaskManager.Application.Services.OrgMember;

public class OrgMemberService(AppDbContext dbContext) : IOrgMemberService
{
    public async Task<Result<List<OrgMemberResponse>>> GetAllByOrgAsync(long orgId, long callerUserId, CancellationToken ct)
    {
        if (!await IsMemberAsync(orgId, callerUserId, ct)) return OrgMemberErrors.Forbidden;

        var items = await dbContext.OrgMembers
            .AsNoTracking()
            .Where(m => m.OrganizationId == orgId && !m.IsDeleted)
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

        return items;
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

        var alreadyMember = await dbContext.OrgMembers
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == orgId && m.UserId == request.UserId && !m.IsDeleted, ct);
        if (alreadyMember) return OrgMemberErrors.AlreadyMember;

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
