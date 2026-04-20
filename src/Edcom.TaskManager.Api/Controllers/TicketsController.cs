using Edcom.TaskManager.Api.Controllers.Common;
using Edcom.TaskManager.Application.Services.Ticket;
using Edcom.TaskManager.Application.Services.Ticket.Contracts;

namespace Edcom.TaskManager.Api.Controllers;

[ApiController]
[Route("api/spaces/{spaceId:long}/tickets")]
public class TicketsController(ITicketService ticketService) : AuthorizedController
{
    /// <summary>Get all tickets in a space.</summary>
    [HttpGet]
    public async Task<IResult> GetAllAsync(long spaceId, CancellationToken ct = default)
    {
        var result = await ticketService.GetAllBySpaceAsync(spaceId, ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Get ticket by id.</summary>
    [HttpGet("{id:long}")]
    public async Task<IResult> GetByIdAsync(long spaceId, long id, CancellationToken ct = default)
    {
        var result = await ticketService.GetByIdAsync(id, ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Create a ticket in the space.</summary>
    [HttpPost]
    public async Task<IResult> AddAsync(long spaceId, [FromBody] CreateTicketRequest request, CancellationToken ct = default)
    {
        var result = await ticketService.AddAsync(spaceId, request, UserId, ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Update ticket.</summary>
    [HttpPut("{id:long}")]
    public async Task<IResult> UpdateAsync(long spaceId, long id, [FromBody] UpdateTicketRequest request, CancellationToken ct = default)
    {
        var result = await ticketService.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Delete ticket (soft delete).</summary>
    [HttpDelete("{id:long}")]
    public async Task<IResult> DeleteAsync(long spaceId, long id, CancellationToken ct = default)
    {
        var result = await ticketService.DeleteAsync(id, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }
}
