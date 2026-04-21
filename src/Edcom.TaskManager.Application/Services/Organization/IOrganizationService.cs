using Edcom.TaskManager.Application.Services.Organization.Contracts;
using Edcom.TaskManager.Domain.Abstractions;

namespace Edcom.TaskManager.Application.Services.Organization;

public interface IOrganizationService
{
    Task<Result<PagedList<OrganizationResponse>>> GetAllAsync(long callerUserId, OrganizationFilterRequest filter, CancellationToken cancellationToken);

    Task<Result<OrganizationResponse>> GetByIdAsync(long id, long callerUserId, CancellationToken cancellationToken);

    Task<Result<OrganizationResponse>> AddAsync(CreateOrganizationRequest request, long createdByUserId, CancellationToken cancellationToken);

    Task<Result> UpdateAsync(long id, UpdateOrganizationRequest request, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(long id, CancellationToken cancellationToken);
}
