using AutoTest.Core.Target.Http;

namespace AutoTest.Application.Dto;

public class HttpBodyDto
{
    public BodyType Type { get; set; }
    public object? Value { get; set; }
    public string? ContentType { get; set; }
}
