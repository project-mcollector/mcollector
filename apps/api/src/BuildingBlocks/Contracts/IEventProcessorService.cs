namespace Contracts;

public interface IEventProcessorService
{
    /// <summary>
    /// Processes a single "raw" event received from the queue.
    /// </summary>
    /// <param name="rawEvent">Event DTO received from Kafka.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    Task ProcessEventAsync(IncomingEventDto rawEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Data Transfer Object (DTO) representing an event as it arrives
/// from the Ingestion API into the Kafka queue.
/// </summary>
public class IncomingEventDto
{
    public Guid EventId { get; set; }
    public Guid ProjectId { get; set; }
    public string EventType { get; set; } // "track", "page", "identify"
    public string? UserId { get; set; }
    public string AnonymousId { get; set; }
    public string EventName { get; set; }
    public DateTime Timestamp { get; set; }
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
    public Dictionary<string, object> Properties { get; set; }
    public Dictionary<string, object>? Traits { get; set; } // Only for identify
}
