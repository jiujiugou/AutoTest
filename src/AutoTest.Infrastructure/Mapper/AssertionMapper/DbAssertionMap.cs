using System.Text.Json;
using AutoTest.Application;
using AutoTest.Application.Dto;
using AutoTest.Assertion.Db;
using AutoTest.Assertions;
using AutoTest.Core.Assertion;

namespace AutoTest.Infrastructure.Mapper.AssertionMapper;

/// <summary>
/// DB 断言映射器：将断言规则的配置 JSON 转换为可执行的数据库断言对象。
/// </summary>
public sealed class DbAssertionMap : IAssertionMap
{
    /// <summary>
    /// 映射器支持的断言类型标识。
    /// </summary>
    public string Type => "DB";

    private readonly IEnumerable<IField> _resolvers;
    private readonly IOperator _operator;

    /// <summary>
    /// 初始化 <see cref="DbAssertionMap"/>。
    /// </summary>
    /// <param name="resolvers">字段解析器集合。</param>
    /// <param name="op">断言操作符集合。</param>
    public DbAssertionMap(IEnumerable<IField> resolvers, IOperator op)
    {
        _resolvers = resolvers;
        _operator = op;
    }

    /// <summary>
    /// 将断言规则映射为断言实例。
    /// </summary>
    /// <param name="rule">断言规则。</param>
    /// <returns>断言实例。</returns>
    public IAssertion Map(AssertionRule rule)
    {
        var dto = JsonSerializer.Deserialize<DbAssertionDto>(rule.ConfigJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

        var field = Enum.Parse<DbAssertionField>(dto.Field, true);

        return new DbAssertion(
            dto.Id,
            field,
            dto.Expected,
            _resolvers,
            _operator,
            rowIndex: dto.RowIndex,
            columnName: dto.ColumnName
        );
    }
}
