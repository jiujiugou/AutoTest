using AutoTest.Core.Dsl;

namespace AutoTest.Workflow;

/// <summary>
/// DSL 模板解析器：将 JSON 模板 + 变量表解析为 <see cref="StepSequence"/>（DAG）。
/// 步骤：Schema 校验 → 变量替换 → 构建 DAG。
/// </summary>
public interface IDslParser
{
    /// <param name="templateJson">模板目标（MonitorTarget）的原始 JSON。</param>
    /// <param name="variables">模板变量键值对。</param>
    Task<StepSequence> ParseAsync(string templateJson, Dictionary<string, string> variables);
}
