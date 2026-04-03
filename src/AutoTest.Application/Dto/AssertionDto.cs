namespace AutoTest.Application.Dto;

public class AssertionDto
{
    public Guid Id { get; set; }

    /// <summary>查询 Assertion 表时由 Dapper 映射；API 请求体可省略。</summary>
    public Guid MonitorId { get; set; }

    public string Type { get; set; } = null!;       // Http/Tcp/Db
    public string ConfigJson { get; set; } = null!; // 配置 JSON
}
