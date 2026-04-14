using AutoTest.Core;

namespace AutoTest.Application;

/// <summary>
/// 目标配置映射接口：将存储/传输的 JSON 配置映射为领域目标 <see cref="MonitorTarget"/>。
/// </summary>
public interface ITargetMap
{
    /// <summary>
    /// 目标类型标识（与 <see cref="MonitorTarget.Type"/> 对应）。
    /// </summary>
    string Type { get; }

    /// <summary>
    /// 将 JSON 配置映射为具体目标实例。
    /// </summary>
    /// <param name="json">目标配置 JSON。</param>
    MonitorTarget Map(string json);
}
