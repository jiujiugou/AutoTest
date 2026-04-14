namespace AutoTest.Application.Dto;

/// <summary>
/// 监控任务 DTO，用于 API/前端与 Application 层之间的数据传输。
/// </summary>
public class MonitorDto
{
    /// <summary>
    /// 监控任务 ID（数据库主键）。
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 监控任务名称。
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// 目标类型标识（如 HTTP/TCP/DB/PYTHON）。
    /// </summary>
    public string TargetType { get; set; } = null!;

    /// <summary>
    /// 目标配置 JSON（与 <see cref="TargetType"/> 对应）。
    /// </summary>
    public string TargetConfig { get; set; } = null!;

    /// <summary>
    /// 是否启用该任务。
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 状态值（对应 <c>MonitorStatus</c> 枚举的数值）。
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// 最近一次执行时间（UTC）。
    /// </summary>
    public DateTime? LastRunTime { get; set; }

    /// <summary>
    /// 断言列表。
    /// </summary>
    public List<AssertionDto> Assertions { get; set; } = new();

    /// <summary>
    /// 是否启用每日自动执行。
    /// </summary>
    public bool AutoDailyEnabled { get; set; }

    /// <summary>
    /// 每日自动执行时间（HH:mm 字符串）。
    /// </summary>
    public string? AutoDailyTime { get; set; }

    /// <summary>
    /// 最大自动执行次数（为空表示不限制）。
    /// </summary>
    public int? MaxRuns { get; set; }

    /// <summary>
    /// 已执行次数。
    /// </summary>
    public int ExecutedCount { get; set; }
}
