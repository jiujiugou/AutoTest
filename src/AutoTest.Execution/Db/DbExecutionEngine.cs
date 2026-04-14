using AutoTest.Core;
using AutoTest.Core.Execution;
using AutoTest.Core.Target.Db;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Npgsql;
using System.Diagnostics;

namespace AutoTest.Execution.Db
{
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
            if (target is not DbTarget dbTarget)
                throw new InvalidOperationException();

            _logger.LogInformation("开始执行 DB 任务: Type={DbType}, SQL={Sql}", dbTarget.DbType, dbTarget.Sql);

            var sw = Stopwatch.StartNew();

            Func<string, string, Task<List<Dictionary<string, object>>>> executeSqlAsync = dbTarget.DbType.ToLower() switch
            {
                "sqlserver" => ExecuteSqlServerAsync,
                "mysql" => ExecuteMySqlAsync,
                "postgresql" => ExecutePostgresAsync,
                _ => throw new NotSupportedException($"不支持的数据库类型: {dbTarget.DbType}")
            };

            try
            {
                var rows = await executeSqlAsync(dbTarget.Sql, dbTarget.ConnectionString);

                sw.Stop();
                _logger.LogInformation("DB 执行成功: Rows={RowCount}, 耗时={Elapsed}ms",
                    rows?.Count ?? 0,
                    sw.ElapsedMilliseconds);
                return new DbExecutionResult(true, "执行成功")
                {
                    Rows = rows,
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "DB 执行失败: Type={DbType}, SQL={Sql}, 耗时={Elapsed}ms",
                    dbTarget.DbType,
                    dbTarget.Sql,
                    sw.ElapsedMilliseconds);
                return new DbExecutionResult(false, $"DB 执行失败: {ex.Message}");
            }


        }

        // ----------------- SQL Server -----------------
        private async Task<List<Dictionary<string, object>>> ExecuteSqlServerAsync(string sql, string connStr)
        {
            _logger.LogDebug("SQLServer: 打开连接");

            var rows = new List<Dictionary<string, object>>();
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            using var tran = conn.BeginTransaction();
            using var cmd = new SqlCommand(sql, conn, tran);

            _logger.LogDebug("SQLServer: 开始执行 SQL");

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.GetValue(i);
                rows.Add(row);
            }

            tran.Commit();
            _logger.LogDebug("SQLServer: 执行完成");

            return rows;
        }

        // ----------------- MySQL -----------------
        private async Task<List<Dictionary<string, object>>> ExecuteMySqlAsync(string sql, string connStr)
        {
            _logger.LogDebug("MySQL: 打开连接");

            var rows = new List<Dictionary<string, object>>();
            using var conn = new MySqlConnection(connStr);
            await conn.OpenAsync();

            using var tran = await conn.BeginTransactionAsync();
            using var cmd = new MySqlCommand(sql, conn, tran);

            _logger.LogDebug("MySQL: 开始执行 SQL");

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.GetValue(i);
                rows.Add(row);
            }

            await tran.CommitAsync();
            _logger.LogDebug("MySQL: 执行完成");

            return rows;
        }

        // ----------------- PostgreSQL -----------------
        private async Task<List<Dictionary<string, object>>> ExecutePostgresAsync(string sql, string connStr)
        {
            _logger.LogDebug("PostgreSQL: 打开连接");

            var rows = new List<Dictionary<string, object>>();
            using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            using var tran = await conn.BeginTransactionAsync();
            using var cmd = new NpgsqlCommand(sql, conn, tran);

            _logger.LogDebug("PostgreSQL: 开始执行 SQL");

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.GetValue(i);
                rows.Add(row);
            }

            await tran.CommitAsync();
            _logger.LogDebug("PostgreSQL: 执行完成");

            return rows;
        }
    }
}
