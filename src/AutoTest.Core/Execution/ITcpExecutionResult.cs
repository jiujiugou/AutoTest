namespace AutoTest.Core.Execution;

public interface ITcpExecutionResult
{
    bool Connected { get; }          // TCP 连接是否成功
    string Response { get; }         // 返回的消息内容
    double LatencyMs { get; }           // 请求-响应延迟（毫秒）
    bool SequenceCorrect { get; }    // 多条消息是否按顺序到达
}
