namespace Edcom.TaskManager.Application.Services.TicketComment.Contracts;

public class CreateTicketCommentRequest
{
    public string Content { get; set; } = string.Empty;
    public long? ParentId { get; set; }
}
