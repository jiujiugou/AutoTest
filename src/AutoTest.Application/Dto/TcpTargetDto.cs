namespace AutoTest.Application.Dto;

/// <summary>
/// TCP 监控目标 DTO。
/// </summary>
public class TcpTargetDto
{
    /// <summary>
    /// 目标主机名或 IP。
    /// </summary>
    public string Host { get; set; } = null!;

    /// <summary>
    /// 目标端口。
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// 超时时间（秒）。
    /// </summary>
    public int Timeout { get; set; }

    /// <summary>
    /// 待发送的消息序列。
    /// </summary>
    public List<string> Messages { get; set; } = new List<string>();
}
