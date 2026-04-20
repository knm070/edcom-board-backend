namespace Edcom.TaskManager.Application.Services.Epic.Contracts;

public class UpdateEpicRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#7F77DD";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
