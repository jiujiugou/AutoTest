using System.Data;
using AutoTest.Core.Abstraction;
using AutoTest.Core.Assertion;
using AutoTest.Core.Execution;
using Dapper;

namespace AutoTest.Infrastructure;

public class ExecutionRecordRepository : IExecutionRecordRepository
{
    private readonly IDbConnection _dbConnection;

    public ExecutionRecordRepository(IDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

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

    public Task<ExecutionRecord?> GetLatestByMonitorIdAsync(Guid monitorId)
    {
        const string sql = """
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

    public Task<IEnumerable<ExecutionRecord>> GetByMonitorIdAsync(Guid monitorId, int take = 20)
    {
        const string sql = """
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

    private async Task<IEnumerable<AssertionResult>> GetAssertionResultsInternalAsync(string sql, Guid executionId)
    {
        var rows = await _dbConnection.QueryAsync<AssertionResultRow>(sql, new { ExecutionId = executionId });
        return rows.Select(r => new AssertionResult(
            Guid.Parse(r.AssertionId),
            r.Target,
            r.IsSuccess != 0,
            r.Actual,
            r.Expected,
            r.Message
        ));
    }

    private sealed class AssertionResultRow
    {
        public string AssertionId { get; set; } = null!;
        public string Target { get; set; } = null!;
        public long IsSuccess { get; set; }
        public string? Actual { get; set; }
        public string? Expected { get; set; }
        public string? Message { get; set; }
    }
}
