namespace Edcom.TaskManager.Application.Services.User;

public static class UserErrors
{
    public static readonly Error NotFound = Error.NotFound("User.NotFound");
    public static readonly Error EmailAlreadyExists = Error.Conflict("User.EmailAlreadyExists");
    public static readonly Error CannotDeactivateSelf = Error.Failure("User.CannotDeactivateSelf");
    public static readonly Error CannotRemoveOwnAdmin = Error.Failure("User.CannotRemoveOwnAdmin");
    public static readonly Error CannotDeleteSelf = Error.Failure("User.CannotDeleteSelf");
}
