using System.Text.Json;
using AutoTest.Application;
using AutoTest.Application.Dto;
using AutoTest.Assertions.Tcp;
using AutoTest.Core.Assertion;

namespace AutoTest.Infrastructure.Mapper.AssertionMapper;

/// <summary>
/// TCP 断言映射器：将断言规则的配置 JSON 转换为可执行的 TCP 断言对象。
/// </summary>
public sealed class TcpAssertionMap : IAssertionMap
{
    /// <summary>
    /// 映射器支持的断言类型标识。
    /// </summary>
    public string Type => "TCP";

    /// <summary>
    /// 将断言规则映射为断言实例。
    /// </summary>
    /// <param name="rule">断言规则。</param>
    /// <returns>断言实例。</returns>
    public IAssertion Map(AssertionRule rule)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var dto = JsonSerializer.Deserialize<TcpAssertionDto>(rule.ConfigJson, options)!;

        if (!Enum.TryParse<TcpAssertionField>(dto.Field, true, out var field))
            throw new InvalidOperationException($"Invalid TCP assertion field: {dto.Field}");
        if (!Enum.TryParse<TcpAssertionOperator>(dto.Operator, true, out var op))
            throw new InvalidOperationException($"Invalid TCP assertion operator: {dto.Operator}");

        return new TcpAssertion(
            dto.Id,
            field,
            op,
            dto.Expected
        );
    }
}
