namespace Edcom.TaskManager.Application.Services.WorkflowStatus;

public static class WorkflowStatusErrors
{
    public static readonly Error NotFound = Error.NotFound("WorkflowStatus.NotFound");
    public static readonly Error Forbidden = Error.Failure("WorkflowStatus.Forbidden");
}
