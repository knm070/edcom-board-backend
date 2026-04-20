namespace Edcom.TaskManager.Application.Services.Space;

public static class SpaceErrors
{
    public static readonly Error NotFound = Error.NotFound("Space.NotFound");
    public static readonly Error SlugAlreadyExists = Error.Conflict("Space.SlugAlreadyExists");
    public static readonly Error Forbidden = Error.Failure("Space.Forbidden");
}
