using System.Text.Json;

namespace AutoTest.Core.Dsl;

/// <summary>
/// 变量替换：将 JSON 中的 <c>{{varName}}</c> / <c>{{varName:default}}</c> 占位符替换为实际值。
/// 模板变量解析阶段和执行阶段都需要使用。
/// </summary>
public interface IVariableResolver
{
    /// <summary>
    /// 替换 JSON 字符串中的模板变量占位符。
    /// </summary>
    /// <exception cref="InvalidOperationException">变量未提供值且无默认值时抛出。</exception>
    string ReplaceJson(string json, Dictionary<string, string> variables);
}

/// <summary>
/// 从步骤响应中按规则提取变量值，供后续步骤引用。
/// </summary>
public interface IResponseValueExtractor
{
    Task<Dictionary<string, string>> ExtractAsync(
        string body,
        IReadOnlyDictionary<string, string[]> headers,
        List<ValueExtractor> extractors);
}
