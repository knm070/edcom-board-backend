using Edcom.TaskManager.Domain.Abstractions;
using Edcom.TaskManager.Domain.Enums;

namespace Edcom.TaskManager.Application.Services.Space.Contracts;

public record SpaceFilterRequest : DataQueryRequest
{
    public BoardType? BoardType { get; init; }
    public bool?      IsActive  { get; init; }
}
