using AutoTest.Assertions;
using AutoTest.Core;
using AutoTest.Core.Assertion;
using Microsoft.Extensions.Logging;

namespace AutoTest.Assertion.Db
{
    public class DbAssertion : IAssertion
    {
        public Guid Id { get; init; }

        public DbAssertionField Field { get; }
        public int RowIndex { get; set; }       // RowValue 专用
        public string ColumnName { get; set; }  // RowValue 专用
        public string Expected { get; }

        private readonly ILogger<DbAssertion>? _logger;
        private readonly IEnumerable<IField> _resolvers;
        private readonly IOperator _operator;

        public DbAssertion(
            Guid id,
            DbAssertionField field,
            string expected,
            IEnumerable<IField> resolvers,
            IOperator op,
            ILogger<DbAssertion>? logger = null,
            int rowIndex = 0,
            string columnName = "")
        {
            Id = id;
            Field = field;
            Expected = expected;
            _resolvers = resolvers;
            _operator = op;
            _logger = logger;
            RowIndex = rowIndex;
            ColumnName = columnName;
        }

        public Task<AssertionResult> EvaluateAsync(ExecutionResult executionResult)
        {
            var resolver = _resolvers.FirstOrDefault(r => r.CanResolve(executionResult));
            if (resolver == null)
                return Task.FromResult(Fail($"No resolver found for field {Field}"));

            var actual = resolver.Resolve(Field, executionResult, RowIndex, ColumnName);

            var success = _operator.Evaluate(actual, Expected);

            return Task.FromResult(new AssertionResult(
                Id,
                Field == DbAssertionField.RowValue ? $"{Field}[{RowIndex}].{ColumnName}" : Field.ToString(),
                success,
                actual?.ToString(),
                Expected,
                success ? "OK" : $"Assertion failed: actual={actual}, expected={Expected}, operator={_operator}"
            ));
        }

        private AssertionResult Fail(string message, object? actual = null)
        {
            _logger?.LogWarning(
                "Assertion failed: Field={Field}, Operator={Operator}, Expected={Expected}, Actual={Actual}, Message={Message}",
                Field, _operator, Expected, actual, message);

            return new AssertionResult(
                Id,
                Field == DbAssertionField.RowValue ? $"{Field}[{RowIndex}].{ColumnName}" : Field.ToString(),
                false,
                actual?.ToString(),
                Expected,
                message
            );
        }
    }
}
