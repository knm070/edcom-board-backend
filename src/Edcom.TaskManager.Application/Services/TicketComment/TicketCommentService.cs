using Edcom.TaskManager.Application.Services.TicketComment.Contracts;
using Edcom.TaskManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TicketCommentEntity = Edcom.TaskManager.Domain.Entities.TicketComment;

namespace Edcom.TaskManager.Application.Services.TicketComment;

public class TicketCommentService(AppDbContext dbContext) : ITicketCommentService
{
    public async Task<Result<List<TicketCommentResponse>>> GetAllByTicketAsync(long ticketId, CancellationToken ct)
    {
        var items = await dbContext.TicketComments
            .AsNoTracking()
            .Where(c => c.TicketId == ticketId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new TicketCommentResponse
            {
                Id             = c.Id,
                TicketId       = c.TicketId,
                AuthorId       = c.AuthorId,
                AuthorName     = c.Author.FullName,
                AuthorAvatarUrl = c.Author.AvatarUrl,
                ParentId       = c.ParentId,
                Content        = c.Content,
                IsEdited       = c.IsEdited,
                CreatedAt      = c.CreatedAt,
                UpdatedAt      = c.UpdatedAt,
            })
            .ToListAsync(ct);

        return items;
    }

    public async Task<Result<TicketCommentResponse>> AddAsync(long ticketId, CreateTicketCommentRequest request, long callerUserId, CancellationToken ct)
    {
        var ticketExists = await dbContext.Tickets
            .AsNoTracking()
            .AnyAsync(t => t.Id == ticketId && !t.IsDeleted, ct);
        if (!ticketExists) return TicketCommentErrors.NotFound;

        var comment = new TicketCommentEntity
        {
            TicketId = ticketId,
            AuthorId = callerUserId,
            ParentId = request.ParentId,
            Content  = request.Content,
        };

        dbContext.TicketComments.Add(comment);
        await dbContext.SaveChangesAsync(ct);

        var response = await dbContext.TicketComments
            .AsNoTracking()
            .Where(c => c.Id == comment.Id)
            .Select(c => new TicketCommentResponse
            {
                Id              = c.Id,
                TicketId        = c.TicketId,
                AuthorId        = c.AuthorId,
                AuthorName      = c.Author.FullName,
                AuthorAvatarUrl = c.Author.AvatarUrl,
                ParentId        = c.ParentId,
                Content         = c.Content,
                IsEdited        = c.IsEdited,
                CreatedAt       = c.CreatedAt,
                UpdatedAt       = c.UpdatedAt,
            })
            .SingleAsync(ct);

        return response;
    }

    public async Task<Result> UpdateAsync(long id, UpdateTicketCommentRequest request, long callerUserId, CancellationToken ct)
    {
        var comment = await dbContext.TicketComments
            .SingleOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (comment is null) return TicketCommentErrors.NotFound;
        if (comment.AuthorId != callerUserId) return TicketCommentErrors.Forbidden;

        comment.Content  = request.Content;
        comment.IsEdited = true;

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(long id, long callerUserId, CancellationToken ct)
    {
        var comment = await dbContext.TicketComments
            .SingleOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (comment is null) return TicketCommentErrors.NotFound;
        if (comment.AuthorId != callerUserId) return TicketCommentErrors.Forbidden;

        comment.IsDeleted = true;
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }
}
