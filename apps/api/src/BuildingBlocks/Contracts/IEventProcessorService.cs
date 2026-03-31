namespace Contracts;

public interface IEventProcessorService
{
    /// <summary>
    /// Обрабатывает одно "сырое" событие, полученное из очереди
    /// </summary>
    /// <param name="rawEvent">DTO события, поступившего из Kafka.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns></returns>
    Task ProcessEventAsync(IncomingEventDto rawEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Data TransferК Object (DTO), представляющий событие в том виде,
/// в котором оно поступает от Ingestion API в очередь Kafka.
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
    public Dictionary<string, object>? Traits { get; set; } // Только для identify
}
