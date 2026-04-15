namespace Edcom.Domain.Abstractions;

public record Error(string Code, ErrorType Type, string? Message = null)
{
    public static readonly Error None = new(string.Empty, ErrorType.Failure);
    public static readonly Error NullValue = new("Error.NullValue", ErrorType.Failure);

    public static Error NotFound(string code) => new(code, ErrorType.NotFound);
    public static Error Validation(string code, string message) => new(code, ErrorType.Validation, message);
    public static Error Conflict(string code) => new(code, ErrorType.Conflict);
    public static Error Failure(string code) => new(code, ErrorType.Failure);
}

public enum ErrorType
{
    Failure    = 0,
    Validation = 1,
    NotFound   = 2,
    Conflict   = 3,
}
