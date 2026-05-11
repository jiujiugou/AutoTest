using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AutoTest.Core;

/// <summary>
/// 测试计划聚合根：将多个监控任务归组，支持批量执行和结果汇总。
/// MonitorIds 以 JSON 数组存储，保持轻量。
/// </summary>
public class TestPlanEntity : IAggregateRoot
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public List<Guid> MonitorIds { get; private set; } = new();
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private TestPlanEntity()
    {
        Name = null!;
    }

    public TestPlanEntity(
        Guid id,
        string name,
        string? description,
        List<Guid> monitorIds,
        DateTime createdAt,
        DateTime updatedAt)
    {
        Id = id;
        Name = name;
        Description = description;
        MonitorIds = monitorIds;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// 更新计划基础信息。
    /// </summary>
    public void Update(string name, string? description, List<Guid> monitorIds)
    {
        Name = name;
        Description = description;
        MonitorIds = monitorIds;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 序列化 MonitorIds 为 JSON 数组字符串，供仓储持久化。
    /// </summary>
    public string MonitorIdsJson => JsonSerializer.Serialize(MonitorIds);
}
