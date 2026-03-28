namespace Contracts;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
