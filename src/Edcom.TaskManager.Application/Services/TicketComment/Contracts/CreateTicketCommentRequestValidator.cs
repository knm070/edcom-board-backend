using Edcom.TaskManager.Application.Services.TicketComment.Contracts;

namespace Edcom.TaskManager.Application.Services.TicketComment;

public class CreateTicketCommentRequestValidator : AbstractValidator<CreateTicketCommentRequest>
{
    public CreateTicketCommentRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty();
    }
}
