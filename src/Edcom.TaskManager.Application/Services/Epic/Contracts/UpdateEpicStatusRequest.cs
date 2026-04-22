using Edcom.TaskManager.Domain.Enums;

namespace Edcom.TaskManager.Application.Services.Epic.Contracts;

public class UpdateEpicStatusRequest
{
    public EpicStatus Status { get; set; }
}
