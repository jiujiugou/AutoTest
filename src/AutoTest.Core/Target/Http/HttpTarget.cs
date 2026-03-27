using System.Text.Json;
using AutoTest.Core;
using AutoTest.Core.Assertion;
using AutoTest.Core.http;
namespace AutoTest.Core.Target.Http;

public class HttpTarget : MonitorTarget
{
    public RequestMethod Method { get; private set; }

    public string Url { get; private set; } = null!;

    public string? Body { get; private set; }

    public Dictionary<string, string>? Headers { get; private set; }

    public Dictionary<string, string>? Query { get; private set; }
    public List<AssertionResult>? Assertions { get; private set; }
    public int Timeout { get; private set; } = 30;

    public override string Type => "http";

    public HttpTarget(
        RequestMethod method,
        string url,
        string? body = null,
        Dictionary<string, string>? headers = null,
        Dictionary<string, string>? query = null,
        int timeout = 30
    )
    {
        Method = method;
        Url = url;
        Body = body;
        Headers = headers;
        Query = query;
        Timeout = timeout;
    }

    public override string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

}