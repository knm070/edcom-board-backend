using Edcom.TaskManager.Application.Services.Ticket.Contracts;

namespace Edcom.TaskManager.Application.Services.Ticket;

public interface ITicketService
{
    Task<Result<List<TicketResponse>>> GetAllBySpaceAsync(long spaceId, CancellationToken ct);
    Task<Result<TicketResponse>> GetByIdAsync(long id, CancellationToken ct);
    Task<Result<TicketResponse>> AddAsync(long spaceId, CreateTicketRequest request, long callerUserId, CancellationToken ct);
    Task<Result> UpdateAsync(long id, UpdateTicketRequest request, CancellationToken ct);
    Task<Result> DeleteAsync(long id, CancellationToken ct);
}
