namespace AutoTest.Application.Dto;

public sealed class PythonAssertionDto
{
    public Guid Id { get; set; }
    public string Field { get; set; } = null!;
    public string Operator { get; set; } = "Equal";
    public string Expected { get; set; } = null!;
}

