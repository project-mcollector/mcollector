namespace EventProcessor;

using Confluent.Kafka;
using System.Text.Json;
using global::Contracts.Messages;
using Infrastructure.Messaging;

public class Worker(
    ILogger<Worker> logger,
    IConfiguration configuration,
    IServiceProvider serviceProvider) : BackgroundService
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(async () =>
        {
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
                GroupId = configuration["Kafka:GroupId"] ?? "event-processor-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false // Commit manually after processing
            };
            using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();

            var topic = configuration["Kafka:Topic"] ?? "raw-events";
            consumer.Subscribe(topic);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(stoppingToken);

                    if (consumeResult?.Message == null) continue;

                    try
                    {
                        if (logger.IsEnabled(LogLevel.Debug))
                            logger.LogDebug("Received message: {Message}", consumeResult.Message.Value);

                        var rawEvent = JsonSerializer.Deserialize<RawEvent>(consumeResult.Message.Value, _jsonOptions);

                        if (rawEvent != null)
                        {
                            using var scope = serviceProvider.CreateScope();
                            var processor = scope.ServiceProvider.GetRequiredService<IEventConsumer<RawEvent>>();

                            await processor.ConsumeAsync(rawEvent, stoppingToken);
                        }

                        consumer.Commit(consumeResult);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            "Error processing message — NOT committing offset, message will be re-processed");
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing Kafka message");
                }
            }
        }, stoppingToken);
    }
}
