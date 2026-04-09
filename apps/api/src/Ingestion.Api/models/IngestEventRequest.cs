namespace Ingestion.Api.Models;

/// <summary>
/// Raw event payload received from the JS tracking SDK.
/// </summary>
public class IngestEventRequest
{
    public required string EventName { get; set; }
    public required string UserId { get; set; }
    public string? AnonymousId { get; set; }
    public string? SessionId { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
    public DateTimeOffset ClientTimestamp { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
