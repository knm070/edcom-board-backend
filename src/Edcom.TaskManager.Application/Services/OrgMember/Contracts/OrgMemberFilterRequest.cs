using Edcom.TaskManager.Domain.Abstractions;
using Edcom.TaskManager.Domain.Enums;

namespace Edcom.TaskManager.Application.Services.OrgMember.Contracts;

public record OrgMemberFilterRequest : DataQueryRequest
{
    public OrgRole? Role { get; init; }
}
