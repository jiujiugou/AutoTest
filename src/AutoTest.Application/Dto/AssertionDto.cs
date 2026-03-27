namespace AutoTest.Application.Dto;

public class AssertionDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;       // Http/Tcp/Db
    public string ConfigJson { get; set; } = null!; // 配置 JSON
}
