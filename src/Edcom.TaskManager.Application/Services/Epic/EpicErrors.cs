namespace Edcom.TaskManager.Application.Services.Epic;

public static class EpicErrors
{
    public static readonly Error NotFound        = Error.NotFound("Epic.NotFound");
    public static readonly Error Forbidden       = Error.Failure("Epic.Forbidden");
    public static readonly Error HasActiveTickets = Error.Failure("Epic.HasActiveTickets");
    public static readonly Error InvalidStatus   = Error.Validation("Epic.InvalidStatus", "Invalid epic status.");
    public static readonly Error SpaceMismatch   = Error.Validation("Epic.SpaceMismatch", "Epic does not belong to this space.");
}
