using Contracts.Messages;
using Infrastructure.Messaging;

namespace Ingestion.Api.Services;

/// <summary>
/// Handles incoming events from the JS SDK.
/// Publishes raw events to the event bus (Kafka).
/// </summary>
public class IngestionService(IEventPublisher publisher) : IIngestionService
{
    /// <summary>
    /// Publishes a single raw event to the event bus.
    /// </summary>
    public async Task IngestAsync(RawEvent rawEvent, CancellationToken cancellationToken = default)
    {
        await publisher.PublishAsync(rawEvent, cancellationToken);
    }

    /// <summary>
    /// Publishes a batch of raw events to the event bus.
    /// </summary>
    public async Task IngestBatchAsync(IEnumerable<RawEvent> rawEvents, CancellationToken cancellationToken = default)
    {
        foreach (var rawEvent in rawEvents)
            await IngestAsync(rawEvent, cancellationToken);
    }
}

/// <summary>
/// Temporary stub for IEventPublisher until Kafka is configured.
/// Replace with real Kafka implementation when available.
/// </summary>
public class StubEventPublisher : IEventPublisher
{
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : class
    {
        Console.WriteLine($"[STUB] Event published: {typeof(T).Name}");
        return Task.CompletedTask;
    }
}
