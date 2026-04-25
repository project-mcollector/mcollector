using System.Text.Json;
using Confluent.Kafka;
using Contracts.Messages;
using Infrastructure.Messaging;

namespace Ingestion.Api.Services;

public class IngestionService(IEventPublisher publisher) : IIngestionService
{
    public async Task IngestAsync(RawEvent rawEvent, CancellationToken cancellationToken = default)
    {
        await publisher.PublishAsync(rawEvent, cancellationToken);
    }

    public async Task IngestBatchAsync(IEnumerable<RawEvent> rawEvents, CancellationToken cancellationToken = default)
    {
        foreach (var rawEvent in rawEvents)
            await IngestAsync(rawEvent, cancellationToken);
    }
}

public class KafkaEventPublisher(IConfiguration configuration) : IEventPublisher
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
        await producer.ProduceAsync(_topic, new Message<Null, string> { Value = json }, cancellationToken);
    }
}
