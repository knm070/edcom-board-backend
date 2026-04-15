namespace Edcom.Domain.Abstractions;

public record Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    protected Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public static implicit operator Result(Error error) => Failure(error);
}

public record Result<T> : Result
{
    public T? Data { get; }

    private Result(bool isSuccess, Error error, T? data)
        : base(isSuccess, error) => Data = data;

    public static Result<T> Success(T data) => new(true, Error.None, data);
    public new static Result<T> Failure(Error error) => new(false, error, default);

    public static implicit operator Result<T>(T data) => Success(data);
    public static implicit operator Result<T>(Error error) => Failure(error);
}
