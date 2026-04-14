using System.Data;
using AutoTest.Core.Abstraction;
using AutoTest.Core.Assertion;
using AutoTest.Core.Execution;
using Dapper;

namespace AutoTest.Infrastructure;

/// <summary>
/// 执行记录仓储（Dapper）：负责写入执行记录与断言结果，并提供按监控维度的查询与统计能力。
/// </summary>
public class ExecutionRecordRepository : IExecutionRecordRepository
{
    private readonly IDbConnection _dbConnection;

    /// <summary>
    /// 初始化 <see cref="ExecutionRecordRepository"/>。
    /// </summary>
    /// <param name="dbConnection">数据库连接。</param>
    public ExecutionRecordRepository(IDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    /// <summary>
    /// 写入一条执行记录。
    /// </summary>
    /// <param name="record">执行记录。</param>
    /// <param name="tx">可选事务。</param>
    public Task AddAsync(ExecutionRecord record, IDbTransaction? tx = null)
    {
        const string sql = """
                            INSERT INTO ExecutionRecord(
                                Id,
                                MonitorId,
                                Status,
                                StartedAt,
                                FinishedAt,
                                IsExecutionSuccess,
                                ErrorMessage,
                                ResultType,
                                ResultJson
                            )
                            VALUES(
                                @Id,
                                @MonitorId,
                                @Status,
                                @StartedAt,
                                @FinishedAt,
                                @IsExecutionSuccess,
                                @ErrorMessage,
                                @ResultType,
                                @ResultJson
                            )
                            """;

        return _dbConnection.ExecuteAsync(sql, new
        {
            record.Id,
            record.MonitorId,
            Status = (int)record.Status,
            record.StartedAt,
            record.FinishedAt,
            record.IsExecutionSuccess,
            record.ErrorMessage,
            record.ResultType,
            record.ResultJson
        }, tx);
    }

    /// <summary>
    /// 批量写入某次执行的断言结果。
    /// </summary>
    /// <param name="executionId">执行记录 ID。</param>
    /// <param name="results">断言结果集合。</param>
    /// <param name="tx">可选事务。</param>
    public Task AddAssertionResultsAsync(Guid executionId, IEnumerable<AssertionResult> results, IDbTransaction? tx = null)
    {
        const string sql = """
                            INSERT INTO AssertionResult(
                                Id,
                                ExecutionId,
                                AssertionId,
                                Target,
                                IsSuccess,
                                Actual,
                                Expected,
                                Message
                            )
                            VALUES(
                                @Id,
                                @ExecutionId,
                                @AssertionId,
                                @Target,
                                @IsSuccess,
                                @Actual,
                                @Expected,
                                @Message
                            )
                            """;

        var param = results.Select(r => new
        {
            Id = Guid.NewGuid(),
            ExecutionId = executionId,
            AssertionId = r.AssertionId,
            r.Target,
            IsSuccess = r.IsSuccess,
            r.Actual,
            r.Expected,
            r.Message
        });

        return _dbConnection.ExecuteAsync(sql, param, tx);
    }

    /// <summary>
    /// 获取某个监控的最新一条执行记录。
    /// </summary>
    /// <param name="monitorId">监控任务 ID。</param>
    /// <returns>最新执行记录；不存在则返回 null。</returns>
    public Task<ExecutionRecord?> GetLatestByMonitorIdAsync(Guid monitorId)
    {
        var isSqlServer = _dbConnection is Microsoft.Data.SqlClient.SqlConnection;
        var sql = isSqlServer
            ? """
              SELECT TOP 1
                  Id,
                  MonitorId,
                  Status,
                  StartedAt,
                  FinishedAt,
                  IsExecutionSuccess,
                  ErrorMessage,
                  ResultType,
                  ResultJson
              FROM ExecutionRecord
              WHERE MonitorId = @MonitorId
              ORDER BY StartedAt DESC
              """
            : """
              SELECT
                  Id,
                  MonitorId,
                  Status,
                  StartedAt,
                  FinishedAt,
                  IsExecutionSuccess,
                  ErrorMessage,
                  ResultType,
                  ResultJson
              FROM ExecutionRecord
              WHERE MonitorId = @MonitorId
              ORDER BY StartedAt DESC
              LIMIT 1
              """;

        return _dbConnection.QuerySingleOrDefaultAsync<ExecutionRecord>(sql, new { MonitorId = monitorId });
    }

    /// <summary>
    /// 获取某个监控的执行记录列表（按开始时间倒序）。
    /// </summary>
    /// <param name="monitorId">监控任务 ID。</param>
    /// <param name="take">最多返回条数。</param>
    /// <returns>执行记录集合。</returns>
    public Task<IEnumerable<ExecutionRecord>> GetByMonitorIdAsync(Guid monitorId, int take = 20)
    {
        var isSqlServer = _dbConnection is Microsoft.Data.SqlClient.SqlConnection;
        var sql = isSqlServer
            ? """
              SELECT TOP (@Take)
                  Id,
                  MonitorId,
                  Status,
                  StartedAt,
                  FinishedAt,
                  IsExecutionSuccess,
                  ErrorMessage,
                  ResultType,
                  ResultJson
              FROM ExecutionRecord
              WHERE MonitorId = @MonitorId
              ORDER BY StartedAt DESC
              """
            : """
              SELECT
                  Id,
                  MonitorId,
                  Status,
                  StartedAt,
                  FinishedAt,
                  IsExecutionSuccess,
                  ErrorMessage,
                  ResultType,
                  ResultJson
              FROM ExecutionRecord
              WHERE MonitorId = @MonitorId
              ORDER BY StartedAt DESC
              LIMIT @Take
              """;

        return _dbConnection.QueryAsync<ExecutionRecord>(sql, new { MonitorId = monitorId, Take = take });
    }

    /// <summary>
    /// 获取某次执行对应的断言结果列表。
    /// </summary>
    /// <param name="executionId">执行记录 ID。</param>
    /// <returns>断言结果集合。</returns>
    public Task<IEnumerable<AssertionResult>> GetAssertionResultsAsync(Guid executionId)
    {
        const string sql = """
                            SELECT
                                AssertionId,
                                Target,
                                IsSuccess,
                                Actual,
                                Expected,
                                Message
                            FROM AssertionResult
                            WHERE ExecutionId = @ExecutionId
                            ORDER BY Timestamp ASC
                            """;

        return GetAssertionResultsInternalAsync(sql, executionId);
    }

    /// <summary>
    /// 获取某个监控的执行统计（总数/成功/失败/首次/最近）。
    /// </summary>
    /// <param name="monitorId">监控任务 ID。</param>
    /// <returns>统计结果。</returns>
    public async Task<MonitorExecutionStats> GetMonitorExecutionStatsAsync(Guid monitorId)
    {
        const string sql = """
                            SELECT
                                COUNT(1) AS Total,
                                SUM(CASE WHEN IsExecutionSuccess = 1 THEN 1 ELSE 0 END) AS Success,
                                SUM(CASE WHEN IsExecutionSuccess = 0 THEN 1 ELSE 0 END) AS Fail,
                                MIN(StartedAt) AS FirstStartedAt,
                                MAX(StartedAt) AS LastStartedAt
                            FROM ExecutionRecord
                            WHERE MonitorId = @MonitorId
                            """;

        var row = await _dbConnection.QuerySingleAsync<MonitorExecutionStatsRow>(sql, new { MonitorId = monitorId });
        return new MonitorExecutionStats(
            row.Total,
            row.Success,
            row.Fail,
            row.FirstStartedAt,
            row.LastStartedAt);
    }

    /// <summary>
    /// 获取某个监控的失败原因 TopN（按出现次数降序，最近发生时间作为次级排序）。
    /// </summary>
    /// <param name="monitorId">监控任务 ID。</param>
    /// <param name="take">最多返回条数。</param>
    /// <returns>失败原因统计。</returns>
    public Task<IEnumerable<MonitorErrorStat>> GetTopErrorStatsAsync(Guid monitorId, int take = 10)
    {
        var isSqlServer = _dbConnection is Microsoft.Data.SqlClient.SqlConnection;
        var sql = isSqlServer
            ? """
              SELECT TOP (@Take)
                  COALESCE(NULLIF(LTRIM(RTRIM(ErrorMessage)), ''), '(无错误信息)') AS ErrorMessage,
                  COUNT(1) AS Count,
                  MAX(StartedAt) AS LastOccurredAt
              FROM ExecutionRecord
              WHERE MonitorId = @MonitorId AND IsExecutionSuccess = 0
              GROUP BY COALESCE(NULLIF(LTRIM(RTRIM(ErrorMessage)), ''), '(无错误信息)')
              ORDER BY Count DESC, LastOccurredAt DESC
              """
            : """
              SELECT
                  COALESCE(NULLIF(TRIM(ErrorMessage), ''), '(无错误信息)') AS ErrorMessage,
                  COUNT(1) AS Count,
                  MAX(StartedAt) AS LastOccurredAt
              FROM ExecutionRecord
              WHERE MonitorId = @MonitorId AND IsExecutionSuccess = 0
              GROUP BY COALESCE(NULLIF(TRIM(ErrorMessage), ''), '(无错误信息)')
              ORDER BY Count DESC, LastOccurredAt DESC
              LIMIT @Take
              """;

        return _dbConnection.QueryAsync<MonitorErrorStat>(sql, new { MonitorId = monitorId, Take = take });
    }

    private async Task<IEnumerable<AssertionResult>> GetAssertionResultsInternalAsync(string sql, Guid executionId)
    {
        var rows = await _dbConnection.QueryAsync<AssertionResultRow>(sql, new { ExecutionId = executionId });
        return rows.Select(r => new AssertionResult(
            r.AssertionId,
            r.Target,
            r.IsSuccess,
            r.Actual,
            r.Expected,
            r.Message
        ));
    }

    private sealed class AssertionResultRow
    {
        public Guid AssertionId { get; set; }
        public string Target { get; set; } = null!;
        public bool IsSuccess { get; set; }
        public string? Actual { get; set; }
        public string? Expected { get; set; }
        public string? Message { get; set; }
    }

    private sealed class MonitorExecutionStatsRow
    {
        public int Total { get; set; }
        public int Success { get; set; }
        public int Fail { get; set; }
        public DateTime? FirstStartedAt { get; set; }
        public DateTime? LastStartedAt { get; set; }
    }
}
