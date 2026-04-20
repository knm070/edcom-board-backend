using Edcom.TaskManager.Api.Controllers.Common;
using Edcom.TaskManager.Application.Services.OrgMember;
using Edcom.TaskManager.Application.Services.OrgMember.Contracts;

namespace Edcom.TaskManager.Api.Controllers;

[ApiController]
[Route("api/organizations/{orgId:long}/members")]
public class OrgMembersController(IOrgMemberService orgMemberService) : AuthorizedController
{
    /// <summary>Get all members of an organization.</summary>
    [HttpGet]
    public async Task<IResult> GetAllAsync(long orgId, CancellationToken ct = default)
    {
        var result = await orgMemberService.GetAllByOrgAsync(orgId, ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Get org member by id.</summary>
    [HttpGet("{id:long}")]
    public async Task<IResult> GetByIdAsync(long orgId, long id, CancellationToken ct = default)
    {
        var result = await orgMemberService.GetByIdAsync(id, ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Add a user to the organization. Requires OrgManager role.</summary>
    [HttpPost]
    public async Task<IResult> AddAsync(long orgId, [FromBody] AddOrgMemberRequest request, CancellationToken ct = default)
    {
        var result = await orgMemberService.AddAsync(orgId, request, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Update member role. Requires OrgManager role.</summary>
    [HttpPut("{id:long}/role")]
    public async Task<IResult> UpdateRoleAsync(long orgId, long id, [FromBody] UpdateOrgMemberRoleRequest request, CancellationToken ct = default)
    {
        var result = await orgMemberService.UpdateRoleAsync(id, request, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Remove member from organization (soft delete). Requires OrgManager role.</summary>
    [HttpDelete("{id:long}")]
    public async Task<IResult> DeleteAsync(long orgId, long id, CancellationToken ct = default)
    {
        var result = await orgMemberService.DeleteAsync(id, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }
}
