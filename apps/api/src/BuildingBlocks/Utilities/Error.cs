namespace Utilities;

public sealed record Error(string Id, string Description);

public static class Errors
{
    public static Error Unauthorized<TKey>(TKey id)
        => new("Unauthorized", $"User '{id}' is not authorized to perform this action");

    public static Error NotFound<TKey>(string entity, TKey id)
        => new($"{entity}.NotFound", $"{entity} with id '{id}' was not found");

    public static Error Validation(string field, string description) =>
        new($"Validation.{field}", description);

    public static Error Conflict(string description)
        => new("Conflict", description);

    public static Error Internal(string description)
        => new("Internal", description);
}
