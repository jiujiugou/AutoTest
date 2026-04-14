using AutoTest.Core.Assertion;

namespace AutoTest.Application.Builder;

/// <summary>
/// 断言规则映射接口：将前端/存储的 JSON 配置映射为 <see cref="AssertionRule"/>。
/// </summary>
public interface IAssertionRuleMap
{
    /// <summary>
    /// 断言类型标识。
    /// </summary>
    string Type { get; }

    /// <summary>
    /// 将 JSON 配置映射为断言规则。
    /// </summary>
    /// <param name="id">断言 ID。</param>
    /// <param name="json">断言配置 JSON。</param>
    AssertionRule Map(Guid id, string json);
}
