namespace Edcom.TaskManager.Application.Services.Sprint;

public static class SprintErrors
{
    public static readonly Error NotFound = Error.NotFound("Sprint.NotFound");
    public static readonly Error Forbidden = Error.Failure("Sprint.Forbidden");
}
