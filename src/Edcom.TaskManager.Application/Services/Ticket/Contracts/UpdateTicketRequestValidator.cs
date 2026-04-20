using Edcom.TaskManager.Application.Services.Ticket.Contracts;

namespace Edcom.TaskManager.Application.Services.Ticket;

public class UpdateTicketRequestValidator : AbstractValidator<UpdateTicketRequest>
{
    public UpdateTicketRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(512);
        RuleFor(x => x.EstimationHours).GreaterThanOrEqualTo(0).When(x => x.EstimationHours.HasValue);
        RuleFor(x => x.StoryPoints).GreaterThanOrEqualTo(0).When(x => x.StoryPoints.HasValue);
    }
}
