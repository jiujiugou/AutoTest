namespace AutoTest.Assertions.Tcp;

public enum TcpAssertionField
{
    Connected,   // TCP 连接是否成功
    Response,    // TCP 返回的消息内容
    LatencyMs,   // 请求-响应延迟（毫秒）
    SequenceCorrect  // 多条消息或分段数据是否按顺序到达
}