namespace Edcom.TaskManager.Application.Services.Epic;

public static class EpicErrors
{
    public static readonly Error NotFound = Error.NotFound("Epic.NotFound");
    public static readonly Error Forbidden = Error.Failure("Epic.Forbidden");
}
