using System.Text.Json;
using AutoTest.Application;
using AutoTest.Application.Dto;
using AutoTest.Assertion.Python;
using AutoTest.Core.Assertion;

namespace AutoTest.Infrastructure.Mapper.AssertionMapper;

/// <summary>
/// Python 断言映射器：将断言规则的配置 JSON 转换为可执行的 Python 断言对象。
/// </summary>
public sealed class PythonAssertionMap : IAssertionMap
{
    /// <summary>
    /// 映射器支持的断言类型标识。
    /// </summary>
    public string Type => "PYTHON";

    /// <summary>
    /// 将断言规则映射为断言实例。
    /// </summary>
    /// <param name="rule">断言规则。</param>
    /// <returns>断言实例。</returns>
    public IAssertion Map(AssertionRule rule)
    {
        var dto = JsonSerializer.Deserialize<PythonAssertionDto>(rule.ConfigJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

        return new PythonAssertion(
            dto.Id,
            dto.Field,
            dto.Operator,
            dto.Expected
        );
    }
}
