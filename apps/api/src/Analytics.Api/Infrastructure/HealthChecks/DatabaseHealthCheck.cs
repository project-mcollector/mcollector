using Analytics.Api.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Analytics.Api.Infrastructure.HealthChecks;

public class DatabaseHealthCheck(AnalyticsDbContext dbContext) : IHealthCheck
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
