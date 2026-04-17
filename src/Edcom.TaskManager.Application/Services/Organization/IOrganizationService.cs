using Edcom.TaskManager.Application.Services.Organization.Contracts;

namespace Edcom.TaskManager.Application.Services.Organization;

public interface IOrganizationService
{
    Task<Result<List<OrganizationResponse>>> GetAllAsync(CancellationToken cancellationToken);

    Task<Result<OrganizationResponse>> GetByIdAsync(long id, CancellationToken cancellationToken);

    Task<Result> AddAsync(CreateOrganizationRequest request, long createdByUserId, CancellationToken cancellationToken);

    Task<Result> UpdateAsync(long id, UpdateOrganizationRequest request, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(long id, CancellationToken cancellationToken);
}
