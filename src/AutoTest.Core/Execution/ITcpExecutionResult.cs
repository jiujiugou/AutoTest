namespace AutoTest.Core.Execution;

/// <summary>
/// TCP 执行结果的标准视图。
/// </summary>
public interface ITcpExecutionResult
{
    /// <summary>
    /// TCP 连接是否成功。
    /// </summary>
    bool Connected { get; }

    /// <summary>
    /// 返回的消息内容。
    /// </summary>
    string Response { get; }

    /// <summary>
    /// 请求-响应延迟（毫秒）。
    /// </summary>
    double LatencyMs { get; }

    /// <summary>
    /// 多条消息是否按顺序到达。
    /// </summary>
    bool SequenceCorrect { get; }
}
