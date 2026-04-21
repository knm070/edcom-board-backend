using Edcom.TaskManager.Application.Services.Ticket.Contracts;

namespace Edcom.TaskManager.Application.Services.Ticket;

public interface ITicketService
{
    Task<Result<List<TicketResponse>>> GetAllBySpaceAsync(long spaceId, GetTicketsFilterRequest filter, long callerUserId, CancellationToken ct);
    Task<Result<TicketResponse>> GetByIdAsync(long id, long callerUserId, CancellationToken ct);
    Task<Result<TicketResponse>> AddAsync(long spaceId, CreateTicketRequest request, long callerUserId, CancellationToken ct);
    Task<Result> UpdateAsync(long id, UpdateTicketRequest request, long callerUserId, CancellationToken ct);
    Task<Result> DeleteAsync(long id, long callerUserId, CancellationToken ct);
    Task<Result<List<ActivityLogResponse>>> GetActivityAsync(long ticketId, long callerUserId, CancellationToken ct);
}
