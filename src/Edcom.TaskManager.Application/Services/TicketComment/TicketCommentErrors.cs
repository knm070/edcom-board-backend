namespace Edcom.TaskManager.Application.Services.TicketComment;

public static class TicketCommentErrors
{
    public static readonly Error NotFound = Error.NotFound("TicketComment.NotFound");
    public static readonly Error Forbidden = Error.Failure("TicketComment.Forbidden");
}
