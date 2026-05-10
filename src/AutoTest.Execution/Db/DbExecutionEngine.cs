using System.Data;
using System.Data.Common;
using System.Diagnostics;
using AutoTest.Core;
using AutoTest.Core.Execution;
using AutoTest.Core.Target.Db;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Npgsql;

namespace AutoTest.Execution.Db;

public class DbExecutionEngine : IExecutionEngine
{
    private readonly ILogger<DbExecutionEngine> _logger;

    public DbExecutionEngine(ILogger<DbExecutionEngine> logger)
    {
        _logger = logger;
    }

    public bool CanExecute(MonitorTarget target) => target is DbTarget;

    public async Task<ExecutionResult> ExecuteAsync(MonitorTarget target)
    {
        if (target is not DbTarget db)
            throw new InvalidOperationException("Expected DbTarget");

        _logger.LogInformation("DB Execute: Type={DbType}, SQL={Sql}", db.DbType, db.Sql);

        var maxAttempts = db.EnableRetry ? db.RetryCount + 1 : 1;
        Exception? lastEx = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(db.TimeoutSeconds));
                var result = await ExecuteOnceAsync(db, cts.Token);
                _logger.LogInformation("DB success: {Elapsed}ms", result.ElapsedMilliseconds);
                return result;
            }
            catch (Exception ex)
            {
                lastEx = ex;
                if (attempt < maxAttempts && IsTransient(ex))
                {
                    _logger.LogWarning(ex, "DB attempt {Attempt}/{Max} failed, retrying in {Delay}ms",
                        attempt, maxAttempts, db.RetryDelayMs);
                    await Task.Delay(db.RetryDelayMs);
                    continue;
                }
                break;
            }
        }

        _logger.LogError(lastEx!, "DB failed: Type={DbType}, SQL={Sql}", db.DbType, db.Sql);
        return new DbExecutionResult(false, $"DB 执行失败: {lastEx!.Message}");
    }

    private async Task<DbExecutionResult> ExecuteOnceAsync(DbTarget db, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        switch (db.DbType.ToLowerInvariant())
        {
            case "sqlserver":
                using (var conn = new SqlConnection(db.ConnectionString))
                {
                    await conn.OpenAsync(ct);
                    using var cmd = new SqlCommand(db.Sql, conn)
                    {
                        CommandTimeout = db.TimeoutSeconds
                    };
                    return await ExecuteCommandAsync(cmd, db.CommandType, db.Sql, sw, ct);
                }

            case "mysql":
                using (var conn = new MySqlConnection(db.ConnectionString))
                {
                    await conn.OpenAsync(ct);
                    using var cmd = new MySqlCommand(db.Sql, conn)
                    {
                        CommandTimeout = db.TimeoutSeconds
                    };
                    return await ExecuteCommandAsync(cmd, db.CommandType, db.Sql, sw, ct);
                }

            case "postgresql":
                using (var conn = new NpgsqlConnection(db.ConnectionString))
                {
                    await conn.OpenAsync(ct);
                    using var cmd = new NpgsqlCommand(db.Sql, conn)
                    {
                        CommandTimeout = db.TimeoutSeconds
                    };
                    return await ExecuteCommandAsync(cmd, db.CommandType, db.Sql, sw, ct);
                }

            default:
                throw new NotSupportedException($"不支持的数据库类型: {db.DbType}");
        }
    }

    private static async Task<DbExecutionResult> ExecuteCommandAsync(
        DbCommand cmd, SqlCommandType commandType, string sql, Stopwatch sw, CancellationToken ct)
    {
        switch (commandType)
        {
            case SqlCommandType.NonQuery:
            {
                var affected = await cmd.ExecuteNonQueryAsync(ct);
                sw.Stop();
                return new DbExecutionResult(true, "执行成功",
                    affectedRows: affected, sql: sql, elapsedMilliseconds: sw.ElapsedMilliseconds);
            }

            case SqlCommandType.Scalar:
            {
                var scalar = await cmd.ExecuteScalarAsync(ct);
                sw.Stop();
                return new DbExecutionResult(true, "执行成功",
                    sql: sql, elapsedMilliseconds: sw.ElapsedMilliseconds)
                {
                    Scalar = scalar
                };
            }

            default: // Query
            {
                var rows = new List<Dictionary<string, object>>();
                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                        row[reader.GetName(i)] = reader.GetValue(i);
                    rows.Add(row);
                }
                sw.Stop();
                return new DbExecutionResult(true, "执行成功",
                    rows: rows, sql: sql, elapsedMilliseconds: sw.ElapsedMilliseconds);
            }
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is SqlException or MySqlException or NpgsqlException or TimeoutException
        or OperationCanceledException or InvalidOperationException;
}
