namespace AutoTest.Core.AI;

/// <summary>
/// AI 分析结果表
/// 用于存储对 Outbox 事件 / 测试结果的结构化分析结果
/// </summary>
public sealed class AIAnalysis
{
    /// <summary>
    /// 主键
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 关联的执行记录 ID。
    /// 唯一业务查询入口，前端通过 executionId 直接查询。
    /// </summary>
    public Guid ExecutionRecordId { get; init; }
    public Guid? OutboxMessageId { get; init; }

    /// <summary>
    /// 分析类型（例如：TestFailure / ApiError / PerformanceIssue）
    /// </summary>
    public string Type { get; init; } = "";

    /// <summary>
    /// 事件严重级别
    /// low / medium / high / critical
    /// </summary>
    public string Severity { get; init; } = "low";

    /// <summary>
    /// 问题分类（用于统计）
    /// 如：DB_ERROR / NULL_REFERENCE / TIMEOUT
    /// </summary>
    public string Category { get; init; } = "";

    /// <summary>
    /// 根因分析（AI输出核心）
    /// </summary>
    public string RootCause { get; init; } = "";

    /// <summary>
    /// 修复建议（AI输出核心）
    /// </summary>
    public string Suggestion { get; init; } = "";

    /// <summary>
    /// 简要总结（一句话）
    /// </summary>
    public string Summary { get; init; } = "";

    /// <summary>
    /// AI置信度（0~1）
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// 输入给 AI 的原始数据（可追溯）
    /// </summary>
    public string InputJson { get; init; } = "";

    /// <summary>
    /// AI 原始输出（调试/回放用）
    /// </summary>
    public string OutputJson { get; init; } = "";

    /// <summary>
    /// 使用的模型（如 doubao / gpt / dify-flow）
    /// </summary>
    public string Model { get; init; } = "";

    /// <summary>
    /// Prompt版本（用于迭代）
    /// </summary>
    public string PromptVersion { get; init; } = "";

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 处理完成时间
    /// </summary>
    public DateTime? ProcessedAt { get; set; }
}