using System.ComponentModel.DataAnnotations;

namespace Contracts.Messages;

public record RawEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid ProjectId { get; init; }
    public int SchemaVersion { get; init; } = 1;
    [MaxLength(200)]
    [Required]
    public required string EventName { get; init; }
    [MaxLength(100)]
    [Required]
    public required string UserId { get; init; }
    [MaxLength(100)]
    public string? AnonymousId { get; init; }
    [MaxLength(100)]
    public string? SessionId { get; init; }
    public Dictionary<string, object>? Properties { get; init; }
    public DateTimeOffset ClientTimestamp { get; init; }
    public DateTimeOffset ServerTimestamp { get; init; }
    [MaxLength(45)]
    public string? IpAddress { get; init; }
    [MaxLength(1000)]
    public string? UserAgent { get; init; }
}
