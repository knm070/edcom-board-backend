namespace Edcom.TaskManager.Domain.Exceptions;

public class ApiException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
