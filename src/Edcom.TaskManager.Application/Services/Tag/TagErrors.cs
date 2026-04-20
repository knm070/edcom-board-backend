namespace Edcom.TaskManager.Application.Services.Tag;

public static class TagErrors
{
    public static readonly Error NotFound = Error.NotFound("Tag.NotFound");
    public static readonly Error Forbidden = Error.Failure("Tag.Forbidden");
}
