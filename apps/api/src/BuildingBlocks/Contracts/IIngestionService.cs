namespace Contracts.Messages;

/// <summary>
/// Receives raw events from the JS SDK and publishes them to the event bus.
/// </summary>
public interface IIngestionService
{
    /// <summary>
    /// Ingests a single event and publishes it to the event bus.
    /// Called when SDK sends POST /api/v1/ingest/event
    /// </summary>
    Task IngestAsync(RawEvent rawEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ingests a batch of events (max 50) and publishes each to the event bus.
    /// Called when SDK sends POST /api/v1/ingest/batch
    /// </summary>
    Task IngestBatchAsync(IEnumerable<RawEvent> rawEvents, CancellationToken cancellationToken = default);
}
