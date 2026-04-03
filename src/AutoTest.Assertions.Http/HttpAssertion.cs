using AutoTest.Assertions.Http.Operator;
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
        public HttpAssertionOperator Operator { get; }
        public string Expected { get; }
        private readonly ILogger<HttpAssertion>? _logger;
        private readonly IEnumerable<IResolver> _resolvers;
        private readonly IEnumerable<IOperator> _operators;
        public HttpAssertion(Guid id, HttpAssertionField field, HttpAssertionOperator op, string expected, IEnumerable<IResolver> resolvers, IEnumerable<IOperator> operators, ILogger<HttpAssertion>? logger = null)
        {
            Id = id;
            Field = field;
            Operator = op;
            Expected = expected;
            _resolvers = resolvers;
            _operators = operators;
            _logger = logger;
        }

        public Task<AssertionResult> EvaluateAsync(ExecutionResult executionResult)
        {
            if (executionResult is not IHttpExecutionResult httpResult)
                throw new ArgumentException("Execution result is not an HTTP execution result.");

            var resolver = _resolvers.FirstOrDefault(r => r.CanResolve(Field.ToString()));
            if (resolver == null)
                return Task.FromResult(Fail($"No resolver found for field {Field}"));

            var actual = resolver.Resolve(Field.ToString(), httpResult);

            var op = _operators.FirstOrDefault(o => o.CanHandle(Operator));
            if (op == null)
                return Task.FromResult(Fail($"No operator found for {Operator}"));

            var success = op.Evaluate(actual, Expected);

            return Task.FromResult(new AssertionResult(
                Id,
                Field.ToString(),
                success,
                actual?.ToString(),
                Expected,
                success
                    ? "OK"
                    : $"Assertion failed: actual={actual}, expected={Expected}, operator={Operator}"
            ));
        }
        private AssertionResult Fail(string message, object? actual = null)
        {
            _logger?.LogWarning(
                "Assertion failed: Field={Field}, Operator={Operator}, Expected={Expected}, Actual={Actual}, Message={Message}",
                Field, Operator, Expected, actual, message);

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
