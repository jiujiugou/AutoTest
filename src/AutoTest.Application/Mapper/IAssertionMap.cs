using AutoTest.Core.Assertion;

namespace AutoTest.Application;

/// <summary>
/// 断言映射接口：将存储的 <see cref="AssertionRule"/> 映射为可执行断言 <see cref="IAssertion"/>。
/// </summary>
public interface IAssertionMap
{
    /// <summary>
    /// 断言类型标识（与 <see cref="AssertionRule.Type"/> 对应）。
    /// </summary>
    string Type { get; }

    /// <summary>
    /// 将断言规则映射为可执行断言实例。
    /// </summary>
    /// <param name="rule">断言规则。</param>
    IAssertion Map(AssertionRule rule);
}
