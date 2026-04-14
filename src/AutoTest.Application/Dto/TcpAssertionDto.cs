namespace AutoTest.Application.Dto;

public class TcpAssertionDto
{
    public Guid Id { get; set; }
    public string Field { get; set; } = null!;
    public string Operator { get; set; } = null!;
    public string Expected { get; set; } = null!;
}
