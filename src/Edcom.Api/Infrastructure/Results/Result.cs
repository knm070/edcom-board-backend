namespace Edcom.Api.Infrastructure.Results;

/// <summary>
/// Base result type for service layer operations.
/// Enables explicit error handling without exceptions for business errors.
/// </summary>
public abstract record Result
{
    /// <summary>Operation completed successfully.</summary>
    public sealed record Success : Result;

    /// <summary>Business logic error (e.g., validation failed).</summary>
    public sealed record Failure(string Code, string Message, object? Data = null) : Result;

    /// <summary>User lacks authorization for operation.</summary>
    public sealed record Unauthorized(string Message) : Result;

    /// <summary>Requested resource not found.</summary>
    public sealed record NotFound(string Entity) : Result;

    /// <summary>Resource state conflict (e.g., status cannot transition).</summary>
    public sealed record Conflict(string Message) : Result;

    /// <summary>Match against result state (pattern matching).</summary>
    public virtual TResult Match<TResult>(
        Func<Success, TResult> onSuccess,
        Func<Failure, TResult> onFailure,
        Func<Unauthorized, TResult> onUnauthorized,
        Func<NotFound, TResult> onNotFound,
        Func<Conflict, TResult> onConflict) =>
        this switch
        {
            Success s => onSuccess(s),
            Failure f => onFailure(f),
            Unauthorized u => onUnauthorized(u),
            NotFound n => onNotFound(n),
            Conflict c => onConflict(c),
            _ => throw new InvalidOperationException($"Unknown result type: {GetType().Name}")
        };
}

/// <summary>Result<T> wraps operation outcome with optional data payload.</summary>
public sealed record Result<T>(Result Outcome, T? Data = default)
{
    /// <summary>Create successful result with data.</summary>
    public static Result<T> Success(T data) => new(new Result.Success(), data);

    /// <summary>Create failure result with business error.</summary>
    public static Result<T> Failure(string code, string message, object? data = null) =>
        new(new Result.Failure(code, message, data), default);

    /// <summary>Create unauthorized result.</summary>
    public static Result<T> Unauthorized(string message) =>
        new(new Result.Unauthorized(message), default);

    /// <summary>Create not found result.</summary>
    public static Result<T> NotFound(string entity) =>
        new(new Result.NotFound(entity), default);

    /// <summary>Create conflict result.</summary>
    public static Result<T> Conflict(string message) =>
        new(new Result.Conflict(message), default);

    /// <summary>Determine if operation succeeded.</summary>
    public bool IsSuccess => Outcome is Result.Success;

    /// <summary>Match against result state and transform to TResult.</summary>
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<Result.Failure, TResult> onFailure,
        Func<Result.Unauthorized, TResult> onUnauthorized,
        Func<Result.NotFound, TResult> onNotFound,
        Func<Result.Conflict, TResult> onConflict) =>
        Outcome.Match(
            _ => onSuccess(Data!),
            onFailure,
            onUnauthorized,
            onNotFound,
            onConflict);

    /// <summary>Throw if operation failed (for internal service chains).</summary>
    public T GetValueOrThrow() =>
        Data ?? throw new InvalidOperationException("Operation failed but no exception was thrown");
}
