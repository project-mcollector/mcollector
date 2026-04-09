namespace Utilities;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }

    protected Result(bool isSuccess, Error? error)
    {
        if (isSuccess && error is not null)
            throw new InvalidOperationException("A successful result cannot carry an error");
        if (!isSuccess && error is null)
            throw new InvalidOperationException("A failed result must carry an error");

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success()
        => new(true, null);

    public static Result Failure(Error error)
        => new(false, error ?? throw new ArgumentNullException(nameof(error)));

    public static implicit operator Result(Error error)
        => Failure(error);

    public static Result<T> Success<T>(T value)
        => new(value);

    public static Result<T> Failure<T>(Error error)
        => new(error);
}

public class Result<T> : Result
{
    public T? Value
    {
        get => IsSuccess
            ? field
            : throw new InvalidOperationException("Cannot access value of a failed result");
        set;
    }

    internal Result(T value) : base(true, null)
        => Value = value;

    internal Result(Error error) : base(false, error)
        => Value = default;

    public static implicit operator Result<T>(T value)
        => new(value);

    public static implicit operator Result<T>(Error error)
        => new(error);
}
