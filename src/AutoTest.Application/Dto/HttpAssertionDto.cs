using AutoTest.Assertions.Http;

namespace AutoTest.Application.Dto;

public class HttpAssertionDto
{
    public Guid Id { get; set; }
    public HttpAssertionField Field { get; set; }
    public HttpAssertionOperator Operator { get; set; }
    public string Expected { get; set; } = null!;
}
