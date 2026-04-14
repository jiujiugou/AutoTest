namespace AutoTest.Application.Dto;

public class HttpAssertionDto
{
    public Guid Id { get; set; }
    public string Field { get; set; } = null!;
    public string Operator { get; set; } = null!;
    public string HeaderKey { get; set; } = null!;
    public string Expected { get; set; } = null!;
}
