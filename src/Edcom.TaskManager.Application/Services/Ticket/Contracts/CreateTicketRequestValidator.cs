using Edcom.TaskManager.Application.Services.Ticket.Contracts;

namespace Edcom.TaskManager.Application.Services.Ticket;

public class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(512);
        RuleFor(x => x.EstimationHours).GreaterThanOrEqualTo(0).When(x => x.EstimationHours.HasValue);
        RuleFor(x => x.StoryPoints).GreaterThanOrEqualTo(0).When(x => x.StoryPoints.HasValue);
    }
}
