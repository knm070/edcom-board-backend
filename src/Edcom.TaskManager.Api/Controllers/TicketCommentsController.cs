using Edcom.TaskManager.Api.Controllers.Common;
using Edcom.TaskManager.Application.Services.TicketComment;
using Edcom.TaskManager.Application.Services.TicketComment.Contracts;

namespace Edcom.TaskManager.Api.Controllers;

[ApiController]
[Route("api/tickets/{ticketId:long}/comments")]
public class TicketCommentsController(ITicketCommentService ticketCommentService) : AuthorizedController
{
    /// <summary>Get all comments for a ticket.</summary>
    [HttpGet]
    public async Task<IResult> GetAllAsync(long ticketId, CancellationToken ct = default)
    {
        var result = await ticketCommentService.GetAllByTicketAsync(ticketId, ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Add a comment to a ticket.</summary>
    [HttpPost]
    public async Task<IResult> AddAsync(long ticketId, [FromBody] CreateTicketCommentRequest request, CancellationToken ct = default)
    {
        var result = await ticketCommentService.AddAsync(ticketId, request, UserId, ct);
        return result.IsSuccess ? Results.Ok(result.Data) : result.ToProblemDetails();
    }

    /// <summary>Update a comment. Only the author can update.</summary>
    [HttpPut("{id:long}")]
    public async Task<IResult> UpdateAsync(long ticketId, long id, [FromBody] UpdateTicketCommentRequest request, CancellationToken ct = default)
    {
        var result = await ticketCommentService.UpdateAsync(id, request, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }

    /// <summary>Delete a comment (soft delete). Only the author can delete.</summary>
    [HttpDelete("{id:long}")]
    public async Task<IResult> DeleteAsync(long ticketId, long id, CancellationToken ct = default)
    {
        var result = await ticketCommentService.DeleteAsync(id, UserId, ct);
        return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
    }
}
