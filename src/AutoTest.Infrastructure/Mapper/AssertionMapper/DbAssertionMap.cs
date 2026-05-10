using System.Text.Json;
using AutoTest.Application;
using AutoTest.Application.Dto;
using AutoTest.Assertion.Db;
using AutoTest.Assertions;
using AutoTest.Core.Assertion;
using Microsoft.Extensions.Logging;

namespace AutoTest.Infrastructure.Mapper.AssertionMapper;

public sealed class DbAssertionMap : IAssertionMap
{
    private readonly IEnumerable<IField> _resolvers;
    private readonly IOperator _operator;
    private readonly ILoggerFactory? _loggerFactory;

    public string Type => "DB";

    public DbAssertionMap(IEnumerable<IField> resolvers, IOperator op, ILoggerFactory? loggerFactory = null)
    {
        _resolvers = resolvers;
        _operator = op;
        _loggerFactory = loggerFactory;
    }

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
            logger: _loggerFactory?.CreateLogger<DbAssertion>(),
            rowIndex: dto.RowIndex,
            columnName: dto.ColumnName
        );
    }
}
