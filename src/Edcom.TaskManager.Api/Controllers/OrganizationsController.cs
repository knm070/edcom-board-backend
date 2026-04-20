using Edcom.TaskManager.Api.Controllers.Common;
using Edcom.TaskManager.Application.Services.Organization;
using Edcom.TaskManager.Application.Services.Organization.Contracts;

namespace Edcom.TaskManager.Api.Controllers;

[ApiController]
[Route("api/organizations")]
public class OrganizationsController(IOrganizationService organizationService) : AuthorizedController
{
    /// <summary>
    /// Get all organizations.
    /// </summary>
    [HttpGet]
    public async Task<IResult> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var result = await organizationService.GetAllAsync(UserId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>
    /// Get organization by id.
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IResult> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var result = await organizationService.GetByIdAsync(id, UserId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>
    /// Create new organization. System admin only.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IResult> AddAsync(
        [FromBody] CreateOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await organizationService.AddAsync(request, UserId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>
    /// Update organization. System admin only.
    /// </summary>
    [HttpPut("{id:long}")]
    [Authorize(Roles = "Admin")]
    public async Task<IResult> UpdateAsync(
        long id,
        [FromBody] UpdateOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await organizationService.UpdateAsync(id, request, cancellationToken);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>
    /// Delete organization (soft delete). System admin only.
    /// </summary>
    [HttpDelete("{id:long}")]
    [Authorize(Roles = "Admin")]
    public async Task<IResult> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var result = await organizationService.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }
}
