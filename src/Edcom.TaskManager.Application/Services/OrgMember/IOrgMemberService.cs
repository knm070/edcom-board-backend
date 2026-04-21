using Edcom.TaskManager.Application.Services.OrgMember.Contracts;
using Edcom.TaskManager.Domain.Abstractions;

namespace Edcom.TaskManager.Application.Services.OrgMember;

public interface IOrgMemberService
{
    Task<Result<PagedList<OrgMemberResponse>>> GetAllByOrgAsync(long orgId, long callerUserId, OrgMemberFilterRequest filter, CancellationToken ct);
    Task<Result<OrgMemberResponse>> GetByIdAsync(long id, long callerUserId, CancellationToken ct);
    Task<Result> AddAsync(long orgId, AddOrgMemberRequest request, long callerUserId, CancellationToken ct);
    Task<Result> UpdateRoleAsync(long id, UpdateOrgMemberRoleRequest request, long callerUserId, CancellationToken ct);
    Task<Result> DeleteAsync(long id, long callerUserId, CancellationToken ct);
}
