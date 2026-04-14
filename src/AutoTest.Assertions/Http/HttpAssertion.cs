using AutoTest.Core;
using AutoTest.Core.Assertion;
using AutoTest.Core.Execution;
using Microsoft.Extensions.Logging;

namespace AutoTest.Assertions.Http
{
    public class HttpAssertion : IAssertion
    {
        public Guid Id { get; init; }

        public HttpAssertionField Field { get; }
        public string HeaderKey { get; set; }
        public string Expected { get; }
        private readonly ILogger<HttpAssertion>? _logger;
        private readonly IEnumerable<IField> _resolvers;
        private readonly IOperator _operators;
        public HttpAssertion(Guid id, HttpAssertionField field, string headerKey,
        string expected, IEnumerable<IField> resolvers, IOperator operators, ILogger<HttpAssertion>? logger = null)
        {
            Id = id;
            Field = field;
            HeaderKey = headerKey;
            Expected = expected;
            _resolvers = resolvers;
            _operators = operators;
            _logger = logger;
        }

        public Task<AssertionResult> EvaluateAsync(ExecutionResult executionResult)
        {

            var resolver = _resolvers.FirstOrDefault(r => r.CanResolve(executionResult));
            if (resolver == null)
                return Task.FromResult(Fail($"No resolver found for field {Field}"));

            var actual = resolver.Resolve(Field, executionResult, HeaderKey);

            var success = _operators.Evaluate(actual, Expected);

            return Task.FromResult(new AssertionResult(
                Id,
                Field.ToString(),
                success,
                actual?.ToString(),
                Expected,
                success
                    ? "OK"
                    : $"Assertion failed: actual={actual}, expected={Expected}, operator={_operators}"
            ));
        }
        private AssertionResult Fail(string message, object? actual = null)
        {
            _logger?.LogWarning(
                "Assertion failed: Field={Field}, Operator={_operators}, Expected={Expected}, Actual={Actual}, Message={Message}",
                Field, _operators, Expected, actual, message);

            return new AssertionResult(
                Id,
                Field.ToString(),
                false,
                actual?.ToString(),
                Expected,
                message
            );
        }
    }
}
