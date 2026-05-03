using System.Text.Json;
using Confluent.Kafka;
using Contracts.Messages;
using Infrastructure.Messaging;

namespace Ingestion.Api.Services;

public class IngestionService(IEventPublisher publisher, ILogger<IngestionService> logger) : IIngestionService
{
    public async Task IngestAsync(RawEvent rawEvent, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Ingesting event {EventId} for project {ProjectId}", rawEvent.EventId, rawEvent.ProjectId);
        await publisher.PublishAsync(rawEvent, cancellationToken);
    }

    public async Task IngestBatchAsync(IEnumerable<RawEvent> rawEvents, CancellationToken cancellationToken = default)
    {
        var batch = rawEvents.ToList();
        logger.LogInformation("Ingesting batch of {Count} events", batch.Count);
        foreach (var rawEvent in batch)
            await IngestAsync(rawEvent, cancellationToken);
    }
}

public class KafkaEventPublisher(IConfiguration configuration, ILogger<KafkaEventPublisher> logger) : IEventPublisher
{
    private readonly string _bootstrapServers =
        configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

    private readonly string _topic =
        configuration["Kafka:Topic"] ?? "raw-events";

    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : class
    {
        var config = new ProducerConfig { BootstrapServers = _bootstrapServers };
        using var producer = new ProducerBuilder<Null, string>(config).Build();

        var json = JsonSerializer.Serialize(message);
        try
        {
            var result = await producer.ProduceAsync(_topic, new Message<Null, string> { Value = json }, cancellationToken);
            logger.LogDebug("Published message to {Topic} [{Partition}@{Offset}]",
                result.Topic, result.Partition.Value, result.Offset.Value);
        }
        catch (ProduceException<Null, string> ex)
        {
            logger.LogError(ex, "Kafka produce failed for topic {Topic}: {Reason}", _topic, ex.Error.Reason);
            throw;
        }
    }
}
