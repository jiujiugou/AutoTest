using AutoTest.Assertions.Http;

namespace AutoTest.Application.Dto;

public class HttpAssertionDto
{
    public Guid Id { get; set; }
    public string Field { get; set; }
    public string Operator { get; set; }
    public string HeaderKey { get; set; } = null!;
    public string Expected { get; set; } = null!;
}
