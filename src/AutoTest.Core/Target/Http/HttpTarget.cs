using AutoTest.Core;
using AutoTest.Core.Assertion;
using AutoTest.Core.http;
namespace AutoTest.Core.Target.Http;

public class HttpTarget : MonitorTarget
{
    public Guid guid { get; private set; }
    public RequestMethod Method { get; private set; }

    public string Url { get; private set; } = null!;

    public string? Body { get; private set; }

    public Dictionary<string, string>? Headers { get; private set; }

    public Dictionary<string, string>? Query { get; private set; }
    public List<AssertionResult>? Assertions { get; private set; }
    public int Timeout { get; private set; } = 30;
}
