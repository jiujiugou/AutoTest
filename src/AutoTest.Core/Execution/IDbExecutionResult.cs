namespace AutoTest.Core.Execution;

public interface IDbExecutionResult
{
    List<Dictionary<string, object>>? Rows { get; }
    int AffectedRows { get; }
    object? Scalar { get; }
    string? Sql { get; }
    long ElapsedMilliseconds { get; }
}
