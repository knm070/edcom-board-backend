using Edcom.TaskManager.Application.Services.TicketComment.Contracts;

namespace Edcom.TaskManager.Application.Services.TicketComment;

public interface ITicketCommentService
{
    Task<Result<List<TicketCommentResponse>>> GetAllByTicketAsync(long ticketId, CancellationToken ct);
    Task<Result<TicketCommentResponse>> AddAsync(long ticketId, CreateTicketCommentRequest request, long callerUserId, CancellationToken ct);
    Task<Result> UpdateAsync(long id, UpdateTicketCommentRequest request, long callerUserId, CancellationToken ct);
    Task<Result> DeleteAsync(long id, long callerUserId, CancellationToken ct);
}
