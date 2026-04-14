using System.Text.Json;
using System.Text.Json.Serialization;
using AutoTest.Core.Assertion;

namespace AutoTest.Core.Target;

/// <summary>
/// TCP 监控目标：描述一次 TCP 连接与消息交互的配置。
/// </summary>
public class TcpTarget : MonitorTarget
{
    /// <summary>
    /// 目标主机名或 IP。
    /// </summary>
    public string Host { get; private set; } = null!;

    /// <summary>
    /// 目标端口。
    /// </summary>
    public int Port { get; private set; }

    /// <summary>
    /// 超时时间（秒）。
    /// </summary>
    public int Timeout { get; private set; }

    /// <summary>
    /// 待发送的消息序列。
    /// </summary>
    public List<string> Messages { get; private set; } = new List<string>();

    /// <summary>
    /// 断言结果（用于与执行结果结构保持一致）。
    /// </summary>
    public List<AssertionResult>? Assertions { get; private set; }

    /// <summary>
    /// 创建 TCP 监控目标。
    /// </summary>
    public TcpTarget(string host, int port, int timeout = 30, List<string>? messages = null)
    {
        Host = host;
        Port = port;
        Timeout = timeout;
        if (messages != null)
        {
            Messages = messages;
        }
    }
    public override string Type => "TCP";

    /// <summary>
    /// 将目标配置序列化为 JSON，用于数据库存储。
    /// </summary>
    public override string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

}
