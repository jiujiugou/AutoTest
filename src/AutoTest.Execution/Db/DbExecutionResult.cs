using AutoTest.Core;
using AutoTest.Core.Execution;

namespace AutoTest.Execution.Db;

public class DbExecutionResult : ExecutionResult, IDbExecutionResult
{
    public List<Dictionary<string, object>>? Rows { get; set; }
    public int AffectedRows { get; set; }
    public object? Scalar { get; set; }
    public string? Sql { get; set; }
    public long ElapsedMilliseconds { get; set; }

    public DbExecutionResult(bool success, string message,
        int affectedRows = 0, List<Dictionary<string, object>>? rows = null, string? sql = null,
        long elapsedMilliseconds = 0) : base(success, message)
    {
        AffectedRows = affectedRows;
        Rows = rows;
        Sql = sql;
        ElapsedMilliseconds = elapsedMilliseconds;
    }
}
