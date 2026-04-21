using Edcom.TaskManager.Domain.Abstractions;

namespace Edcom.TaskManager.Application.Services.Organization.Contracts;

public record OrganizationFilterRequest : DataQueryRequest
{
    public bool? IsActive { get; init; }
}
