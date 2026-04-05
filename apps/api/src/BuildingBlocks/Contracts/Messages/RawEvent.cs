namespace Contracts.Messages;

public record RawEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public int SchemaVersion { get; init; } = 1;
    public required string EventName { get; init; }
    public required string UserId { get; init; }
    public string? AnonymousId { get; init; }
    public string? SessionId { get; init; }
    public Dictionary<string, object>? Properties { get; init; }
    public DateTimeOffset ClientTimestamp { get; init; }
    public DateTimeOffset ServerTimestamp { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}
