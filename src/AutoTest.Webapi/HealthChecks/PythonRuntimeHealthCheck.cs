using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AutoTest.Webapi.HealthChecks;

public class PythonRuntimeHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext ctx, CancellationToken ct)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromSeconds(5));
            return process.ExitCode == 0
                ? HealthCheckResult.Healthy((await process.StandardOutput.ReadToEndAsync(ct)).Trim())
                : HealthCheckResult.Unhealthy($"Exit code: {process.ExitCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Python not available", ex);
        }
    }
}
