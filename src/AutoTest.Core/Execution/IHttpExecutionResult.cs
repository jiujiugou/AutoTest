namespace AutoTest.Core.Execution;

/// <summary>
/// HTTP 执行结果的标准视图。
/// </summary>
/// <remarks>
/// 具体的 HTTP 执行结果类型通常会实现该接口，以便断言与上层逻辑统一读取状态码、响应内容、响应头与耗时。
/// </remarks>
public interface IHttpExecutionResult
{
    /// <summary>
    /// HTTP 状态码，比如 200、404
    /// </summary>
    int StatusCode { get; set; }

    /// <summary>
    /// 响应内容
    /// </summary>
    string? Body { get; set; }

    /// <summary>
    /// 响应头（同名头合并为数组）。
    /// </summary>
    IReadOnlyDictionary<string, string[]> Headers { get; set; }
    /// <summary>
    /// 响应时间
    /// </summary>
    long? ElapsedMilliseconds { get; set; }
}
