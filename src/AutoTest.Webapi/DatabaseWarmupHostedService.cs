using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;

namespace AutoTest.Webapi;

public sealed class DatabaseWarmupHostedService : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseWarmupHostedService> _logger;

    public DatabaseWarmupHostedService(IConfiguration configuration, ILogger<DatabaseWarmupHostedService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var provider = _configuration["Database:Provider"] ?? "SqlServer";
        var defaultCs = _configuration.GetConnectionString("DefaultConnection");
        var hangfireCs = _configuration.GetConnectionString("HangfireConnection");

        if (string.IsNullOrWhiteSpace(defaultCs) && string.IsNullOrWhiteSpace(hangfireCs))
            return;

        try
        {
            await WarmAsync(provider, defaultCs, cancellationToken);
            await WarmAsync(provider, hangfireCs, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database warmup failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task WarmAsync(string provider, string? connectionString, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            await using var conn = new SqliteConnection(connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync(cancellationToken);
        }
        else
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync(cancellationToken);
        }
    }
}

