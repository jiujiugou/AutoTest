namespace AutoTest.Application.Dto;

/// <summary>
/// 断言配置 DTO：用于描述某个监控任务下的一条断言规则。
/// </summary>
public class AssertionDto
{
    /// <summary>
    /// 断言 ID。
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>查询 Assertion 表时由 Dapper 映射；API 请求体可省略。</summary>
    public Guid MonitorId { get; set; }

    /// <summary>
    /// 断言类型标识（如 HTTP/TCP/DB/PYTHON）。
    /// </summary>
    public string Type { get; set; } = null!;

    /// <summary>
    /// 断言配置 JSON（与 <see cref="Type"/> 对应）。
    /// </summary>
    public string ConfigJson { get; set; } = null!;
}
