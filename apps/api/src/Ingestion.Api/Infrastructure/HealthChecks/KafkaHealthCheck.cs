using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ingestion.Api.Infrastructure.HealthChecks;

public class KafkaHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = new AdminClientConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
                SocketTimeoutMs = 5000,
            };
            using var adminClient = new AdminClientBuilder(config).Build();
            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));
            return Task.FromResult(
                metadata.Brokers.Count > 0
                    ? HealthCheckResult.Healthy($"{metadata.Brokers.Count} broker(s) reachable")
                    : HealthCheckResult.Unhealthy("No Kafka brokers reachable"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(ex.Message));
        }
    }
}
