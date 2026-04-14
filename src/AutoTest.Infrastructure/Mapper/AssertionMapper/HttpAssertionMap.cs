using System.Text.Json;
using AutoTest.Application;
using AutoTest.Application.Dto;
using AutoTest.Assertions;
using AutoTest.Assertions.Http;
using AutoTest.Core.Assertion;

namespace AutoTest.Infrastructure.Mapper.AssertionMapper;

/// <summary>
/// HTTP 断言映射器：将断言规则的配置 JSON 转换为可执行的 HTTP 断言对象。
/// </summary>
public sealed class HttpAssertionMap : IAssertionMap
{
    /// <summary>
    /// 映射器支持的断言类型标识。
    /// </summary>
    public string Type => "HTTP";

    private readonly IEnumerable<IField> _resolvers;
    private readonly IOperator _operators;

    /// <summary>
    /// 初始化 <see cref="HttpAssertionMap"/>。
    /// </summary>
    /// <param name="resolvers">HTTP 断言字段解析器集合。</param>
    /// <param name="operators">断言操作符集合。</param>
    public HttpAssertionMap(IEnumerable<IField> resolvers, IOperator operators)
    {
        _resolvers = resolvers;
        _operators = operators;
    }

    /// <summary>
    /// 将断言规则映射为断言实例。
    /// </summary>
    /// <param name="rule">断言规则。</param>
    /// <returns>断言实例。</returns>
    public IAssertion Map(AssertionRule rule)
    {
        var dto = JsonSerializer.Deserialize<HttpAssertionDto>(rule.ConfigJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

        return new HttpAssertion(
            dto.Id,
            Enum.Parse<HttpAssertionField>(dto.Field, true),
            dto.HeaderKey,
            dto.Expected,
            _resolvers,
            _operators
        );
    }
}
