namespace Utilities;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
