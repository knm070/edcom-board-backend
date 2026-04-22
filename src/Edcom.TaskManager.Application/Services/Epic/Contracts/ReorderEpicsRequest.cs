namespace Edcom.TaskManager.Application.Services.Epic.Contracts;

public class ReorderEpicsRequest
{
    public List<long> EpicIds { get; set; } = [];
}
