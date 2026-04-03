using AutoTest.Assertions.Tcp;

namespace AutoTest.Application.Dto;

public class TcpAssertionDto
{
    public Guid Id { get; set; }
    public TcpAssertionField Field { get; set; }
    public TcpAssertionOperator Operator { get; set; }
    public string Expected { get; set; } = null!;
}
