namespace AutoTest.Core.Execution;

public interface IHttpExecutionResult
{
    /// <summary>
    /// HTTP 状态码，比如 200、404
    /// </summary>
    int StatusCode { get; }

    /// <summary>
    /// 响应内容
    /// </summary>
    string Body { get; }

    /// <summary>
    /// 响应时间
    /// </summary>
    long ElapsedMilliseconds { get; }
}
