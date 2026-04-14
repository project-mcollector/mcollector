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
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(async () =>
        {
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
                GroupId = configuration["Kafka:GroupId"] ?? "event-processor-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false // We will commit manually after processing
            };
            using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();

            var topic = configuration["Kafka:Topic"] ?? "raw-events";
            consumer.Subscribe(topic);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(stoppingToken);

                    if (consumeResult?.Message != null)
                    {
                        logger.LogInformation("Received message: {Message}", consumeResult.Message.Value);

                        var rawEvent = JsonSerializer.Deserialize<RawEvent>(consumeResult.Message.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (rawEvent != null)
                        {
                            using var scope = serviceProvider.CreateScope();
                            var processor = scope.ServiceProvider.GetRequiredService<IEventConsumer<RawEvent>>();

                            await processor.ConsumeAsync(rawEvent, stoppingToken);
                        }

                        consumer.Commit(consumeResult);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown
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
