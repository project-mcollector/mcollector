namespace Utilities;

public static class ResultExtensions
{
    public static Result<T> ToSuccessResult<T>(this T value) => Result.Success(value);
    public static Result<T> ToErrorResult<T>(this Error error) => Result.Failure<T>(error);

    public static Result<T> ToResult<T>(this T? value, Error error)
        => value is not null
            ? Result.Success(value)
            : Result.Failure<T>(error);

    public static TResult Match<TResult>(this Result result, Func<TResult> onSuccess, Func<Error, TResult> onFailure)
        => result.IsSuccess
            ? onSuccess()
            : onFailure(result.Error);

    public static TResult Match<T, TResult>(this Result<T> result, Func<T, TResult> onSuccess,
        Func<Error, TResult> onFailure)
        => result.IsSuccess
            ? onSuccess(result.Value)
            : onFailure(result.Error);

    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> mapper)
        => result.IsSuccess
            ? Result.Success(mapper(result.Value))
            : Result.Failure<TOut>(result.Error);

    public static async Task<Result<TOut>> MapAsync<TIn, TOut>(this Task<Result<TIn>> resultTask,
        Func<TIn, TOut> mapper)
    {
        var result = await resultTask;
        return result.IsSuccess
            ? Result.Success(mapper(result.Value))
            : Result.Failure<TOut>(result.Error);
    }

    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> binder)
        => result.IsSuccess
            ? binder(result.Value)
            : Result.Failure<TOut>(result.Error);

    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(this Task<Result<TIn>> resultTask,
        Func<TIn, Task<Result<TOut>>> binder)
    {
        var result = await resultTask;

        return result.IsSuccess
            ? await binder(result.Value)
            : Result.Failure<TOut>(result.Error);
    }

    public static Result<T> Tap<T>(this Result<T> result, Action<T> action)
    {
        if (result.IsSuccess) action(result.Value);
        return result;
    }

    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Error error)
        => result.IsSuccess
            ? predicate(result.Value)
                ? result
                : Result.Failure<T>(error)
            : result;

    public static async Task<Result<T>> Try<T>(Func<Task<T>> func, Func<Exception, Error> errorFactory)
    {
        try
        {
            var value = await func();
            return Result.Success(value);
        }
        catch (Exception ex)
        {
            return Result.Failure<T>(errorFactory(ex));
        }
    }

    public static TResult Finally<T, TResult>(this Result<T> result, Func<Result<T>, TResult> func)
        => func(result);

    public static async Task<TResult> Finally<T, TResult>(this Task<Result<T>> resultTask,
        Func<Result<T>, TResult> func)
        => func(await resultTask);
}
