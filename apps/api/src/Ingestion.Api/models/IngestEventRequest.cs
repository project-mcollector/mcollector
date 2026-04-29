namespace Ingestion.Api.Models;

public class IngestEventRequest
{
    public required string EventName { get; set; }
    public string? UserId { get; set; }
    public string? AnonymousId { get; set; }
    public string? SessionId { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
    public DateTimeOffset ClientTimestamp { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public class SdkBatchRequest
{
    public string WriteKey { get; set; } = string.Empty;
    public List<SdkEventRequest> Events { get; set; } = new();
}

public class SdkEventRequest
{
    public string? MessageId { get; set; }
    public string Event { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? AnonymousId { get; set; }
    public string? SessionId { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public string? UserAgent { get; set; }
}
