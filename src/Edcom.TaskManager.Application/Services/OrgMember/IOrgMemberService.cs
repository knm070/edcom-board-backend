using Edcom.TaskManager.Application.Services.OrgMember.Contracts;

namespace Edcom.TaskManager.Application.Services.OrgMember;

public interface IOrgMemberService
{
    Task<Result<List<OrgMemberResponse>>> GetAllByOrgAsync(long orgId, CancellationToken ct);
    Task<Result<OrgMemberResponse>> GetByIdAsync(long id, CancellationToken ct);
    Task<Result> AddAsync(long orgId, AddOrgMemberRequest request, long callerUserId, CancellationToken ct);
    Task<Result> UpdateRoleAsync(long id, UpdateOrgMemberRoleRequest request, long callerUserId, CancellationToken ct);
    Task<Result> DeleteAsync(long id, long callerUserId, CancellationToken ct);
}
