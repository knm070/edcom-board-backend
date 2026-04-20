using Edcom.TaskManager.Application.Services.TicketComment.Contracts;

namespace Edcom.TaskManager.Application.Services.TicketComment;

public class UpdateTicketCommentRequestValidator : AbstractValidator<UpdateTicketCommentRequest>
{
    public UpdateTicketCommentRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty();
    }
}
