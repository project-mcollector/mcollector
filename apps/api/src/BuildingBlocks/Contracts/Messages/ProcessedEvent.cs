namespace Contracts.Messages;

public record ProcessedEvent
{
    public Guid EventId { get; init; }
    public Guid ProjectId { get; init; }
    public int SchemaVersion { get; init; } = 1;
    public required string EventName { get; init; }
    public required string UserId { get; init; }
    public string? SessionId { get; init; }
    public string? PropertiesJson { get; init; }
    public DateTimeOffset ProcessedAt { get; init; }
}
