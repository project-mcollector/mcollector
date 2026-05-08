using System.Text.Json;
using Confluent.Kafka;
using Contracts.Messages;
using Infrastructure.Messaging;

namespace Ingestion.Api.Services;

public class IngestionService(IEventPublisher publisher, ILogger<IngestionService> logger) : IIngestionService
{
    public async Task IngestAsync(RawEvent rawEvent, CancellationToken cancellationToken = default)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Ingesting event {EventId} for project {ProjectId}", rawEvent.EventId, rawEvent.ProjectId);
        await publisher.PublishAsync(rawEvent, cancellationToken);
    }

    public async Task IngestBatchAsync(IEnumerable<RawEvent> rawEvents, CancellationToken cancellationToken = default)
    {
        var batch = rawEvents.ToList();
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Ingesting batch of {Count} events", batch.Count);
        foreach (var rawEvent in batch)
            await IngestAsync(rawEvent, cancellationToken);
    }
}

public class KafkaEventPublisher : IEventPublisher, IDisposable
{
    private readonly string _topic;
    private readonly IProducer<Null, string> _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;

    public KafkaEventPublisher(IConfiguration configuration, ILogger<KafkaEventPublisher> logger)
    {
        _topic = configuration["Kafka:Topic"] ?? "raw-events";
        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            MessageTimeoutMs = 5000,
            RequestTimeoutMs = 3000,
            SocketTimeoutMs = 3000
        };
        _producer = new ProducerBuilder<Null, string>(config).Build();
        _logger = logger;
    }

    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : class
    {
        var json = JsonSerializer.Serialize(message);
        try
        {
            var result =
                await _producer.ProduceAsync(_topic, new() { Value = json }, cancellationToken);
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Published message to {Topic} [{Partition}@{Offset}]",
                    result.Topic, result.Partition.Value, result.Offset.Value);
        }
        catch (ProduceException<Null, string> ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
                _logger.LogError(ex, "Kafka produce failed for topic {Topic}: {Reason}", _topic, ex.Error.Reason);
            throw;
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _producer.Dispose();
    }
}
