using Microsoft.AspNetCore.Http;

namespace Utilities;

public static class ResultHttpExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
        => result.Match(onSuccess, MapError);

    public static IResult ToHttpResult(this Result result, Func<IResult> onSuccess)
        => result.Match(onSuccess, MapError);

    private static IResult MapError(Error error) => error.Id switch
    {
        var id when id.EndsWith(".NotFound") => Results.NotFound(new { id, error.Description }),
        var id when id.StartsWith("Validation.") => Results.BadRequest(new { id, error.Description }),
        "Unauthorized" => Results.Unauthorized(),
        "Email.NotConfirmed" => Results.Forbid(),
        "Conflict" => Results.Conflict(new { error.Id, error.Description }),
        _ => Results.Problem(detail: error.Description)
    };
}
