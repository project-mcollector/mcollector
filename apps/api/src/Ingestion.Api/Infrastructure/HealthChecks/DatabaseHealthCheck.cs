using Ingestion.Api.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ingestion.Api.Infrastructure.HealthChecks;

public class DatabaseHealthCheck(IdentityValidationContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Database is unreachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}
