namespace Edcom.TaskManager.Application.Services.TicketComment.Contracts;

public class TicketCommentResponse
{
    public long Id { get; set; }
    public long TicketId { get; set; }
    public long AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorAvatarUrl { get; set; }
    public long? ParentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsEdited { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
