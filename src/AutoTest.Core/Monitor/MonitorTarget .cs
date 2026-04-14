namespace AutoTest.Core;

/// <summary>
/// 监控任务的目标定义。
/// </summary>
/// <remarks>
/// 目标对象用于描述“要执行什么检查”（如 HTTP/TCP/DB/Python）。
/// 该类型通常会被持久化到数据库：<see cref="Type"/> 表示目标类型，<see cref="ToJson"/> 表示类型相关配置。
/// </remarks>
public abstract class MonitorTarget
{
    /// <summary>
    /// 目标类型标识，用于数据库存储与运行时路由（如 HTTP/TCP/DB/PYTHON）。
    /// </summary>
    public abstract string Type { get; }

    /// <summary>
    /// 将目标配置序列化为 JSON，用于数据库存储。
    /// </summary>
    public abstract string ToJson();
}
