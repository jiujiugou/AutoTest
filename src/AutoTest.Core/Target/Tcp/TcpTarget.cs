using System.Text.Json;
using System.Text.Json.Serialization;
using AutoTest.Core.Assertion;

namespace AutoTest.Core.Target;

public class TcpTarget : MonitorTarget
{
    public string Host { get; private set; } = null!;
    public int Port { get; private set; }
    public int Timeout { get; private set; }
    public List<string> Messages { get; private set; } = new List<string>();
    public List<AssertionResult>? Assertions { get; private set; }
    public TcpTarget(string host, int port, int timeout = 30)
    {
        Host = host;
        Port = port;
        Timeout = timeout;
    }
    public override string Type => "TCP";

    public override string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

}
