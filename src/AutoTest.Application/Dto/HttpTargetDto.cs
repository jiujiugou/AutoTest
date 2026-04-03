using AutoTest.Core.http;
using AutoTest.Core.Target.Http;

namespace AutoTest.Application.Dto;

public class HttpTargetDto
{
    public RequestMethod Method { get; set; }
    public string Url { get; set; } = null!;
    public HttpBody? Body { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public Dictionary<string, string>? Query { get; set; }
    public int Timeout { get; set; }
}
