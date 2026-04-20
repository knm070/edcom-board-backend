namespace Edcom.TaskManager.Application.Services.Ticket;

public static class TicketErrors
{
    public static readonly Error NotFound = Error.NotFound("Ticket.NotFound");
    public static readonly Error SpaceNotFound = Error.NotFound("Ticket.SpaceNotFound");
}
