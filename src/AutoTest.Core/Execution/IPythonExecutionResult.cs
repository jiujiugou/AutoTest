namespace AutoTest.Core.Execution;

/// <summary>
/// Python 执行结果的标准视图。
/// </summary>
public interface IPythonExecutionResult
{
    /// <summary>
    /// 退出码。
    /// </summary>
    int ExitCode { get; }

    /// <summary>
    /// 标准输出。
    /// </summary>
    string StdOut { get; }

    /// <summary>
    /// 标准错误。
    /// </summary>
    string StdErr { get; }

    /// <summary>
    /// 执行耗时（毫秒）。
    /// </summary>
    long ElapsedMs { get; }

    /// <summary>
    /// 是否发生超时。
    /// </summary>
    bool TimedOut { get; }
}
