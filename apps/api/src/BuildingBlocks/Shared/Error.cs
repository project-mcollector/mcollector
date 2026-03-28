namespace Shared;

public enum ErrorType
{
    Unauthorized
}

public record Error(string Id, ErrorType Type, string Description);

public static class Errors
{
    public static Error Unauthorized { get; } = new("Unauthorized", ErrorType.Unauthorized,
        "User is not authorized to perform this action");
}
