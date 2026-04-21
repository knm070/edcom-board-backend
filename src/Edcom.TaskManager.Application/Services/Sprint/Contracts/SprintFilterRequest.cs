using Edcom.TaskManager.Domain.Abstractions;
using Edcom.TaskManager.Domain.Enums;

namespace Edcom.TaskManager.Application.Services.Sprint.Contracts;

public record SprintFilterRequest : DataQueryRequest
{
    public SprintStatus? Status { get; init; }
}
