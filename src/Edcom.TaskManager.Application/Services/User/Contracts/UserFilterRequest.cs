using Edcom.TaskManager.Domain.Abstractions;

namespace Edcom.TaskManager.Application.Services.User.Contracts;

public record UserFilterRequest : DataQueryRequest
{
    public bool? IsSystemAdmin { get; init; }
    public bool? IsActive      { get; init; }
}
