namespace AutoTest.Application.Dto;

public class MonitorDto
{
    public Guid Id { get; set; }                  // 数据库主键
    public string Name { get; set; } = null!;     // 监控名称

    public string TargetType { get; set; } = null!;   // Target 类型（HTTP/TCP/DB）
    public string TargetConfig { get; set; } = null!; // Target 配置 JSON

    public bool IsEnabled { get; set; } = true;   // 是否启用
    public int Status { get; set; }               // 对应 MonitorStatus 枚举
    public DateTime? LastRunTime { get; set; }    // 最近一次执行时间
    public List<AssertionDto> Assertions { get; set; } = new(); // 断言列表
}
