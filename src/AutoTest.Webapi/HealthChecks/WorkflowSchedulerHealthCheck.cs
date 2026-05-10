using Hangfire;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AutoTest.Webapi.HealthChecks;

public class WorkflowSchedulerHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext ctx, CancellationToken ct)
    {
        try
        {
            var monitor = JobStorage.Current.GetMonitoringApi();
            var servers = monitor.Servers();
            if (servers.Count == 0)
                return HealthCheckResult.Unhealthy("No Hangfire servers running");

            return HealthCheckResult.Healthy($"Hangfire servers: {servers.Count}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Hangfire unreachable", ex);
        }
    }
}
