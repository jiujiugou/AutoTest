using AutoTest.Application.Builder;
using AutoTest.Core.Assertion;

namespace AutoTest.Infrastructure.Mapper.AssertionRuleMapper;

/// <summary>
/// TCP 断言规则映射器：将前端传入的 JSON 配置封装为 <see cref="AssertionRule"/>。
/// </summary>
public sealed class TcpAssertionRuleMap : IAssertionRuleMap
{
    /// <summary>
    /// 映射器支持的断言类型标识。
    /// </summary>
    public string Type => "TCP";

    /// <summary>
    /// 创建断言规则。
    /// </summary>
    /// <param name="id">断言 ID。</param>
    /// <param name="json">断言配置 JSON。</param>
    /// <returns>断言规则。</returns>
    public AssertionRule Map(Guid id, string json)
    {
        return new AssertionRule(id, Type, json);
    }
}
