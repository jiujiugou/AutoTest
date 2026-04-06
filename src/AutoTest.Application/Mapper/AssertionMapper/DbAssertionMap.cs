using AutoTest.Application.Dto;
using AutoTest.Assertion.Db;
using AutoTest.Assertions;
using AutoTest.Core.Assertion;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace AutoTest.Application.Mapper.AssertionMapper
{
    internal class DbAssertionMap:IAssertionMap
    {
        private readonly IEnumerable<IField> _resolvers;
        private readonly IOperator _operator;

        public DbAssertionMap(IEnumerable<IField> resolvers, IOperator op)
        {
            _resolvers = resolvers;
            _operator = op;
        }

        public IAssertion Map(AssertionRule rule)
        {
            var dto = JsonSerializer.Deserialize<DbAssertionDto>(rule.ConfigJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;

            // 枚举转换
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
}
