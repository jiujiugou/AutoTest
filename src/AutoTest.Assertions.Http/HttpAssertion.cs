using AutoTest.Core;
using AutoTest.Core.Assertion;
using AutoTest.Core.Execution;

namespace AutoTest.Assertions.Http
{
    public class HttpAssertion : IAssertion
    {
        public Guid Id { get; init; }

        public HttpAssertionField Field { get; }
        public HttpAssertionOperator Operator { get; }
        public string Expected { get; }

        public HttpAssertion(Guid id, HttpAssertionField field, HttpAssertionOperator op, string expected)
        {
            Id = id;
            Field = field;
            Operator = op;
            Expected = expected;
        }

        public async Task<AssertionResult> EvaluateAsync(ExecutionResult executionResult)
        {
            if (executionResult is not IHttpExecutionResult httpResult)
            {
                return new AssertionResult(
                    Id,
                    Field.ToString(),
                    false,
                    null,
                    Expected,
                    "Execution result is not HttpExecutionResult"
                );
            }

            string? actualValue = Field switch
            {
                HttpAssertionField.StatusCode => httpResult.StatusCode.ToString(),
                HttpAssertionField.Body => httpResult.Body,
                _ => throw new InvalidOperationException($"Unsupported HttpAssertionField: {Field}")
            };

            bool isSuccess = Operator switch
            {
                HttpAssertionOperator.Equal => actualValue == Expected,
                HttpAssertionOperator.Contains => actualValue?.Contains(Expected) == true,
                _ => throw new InvalidOperationException($"Unsupported HttpAssertionOperator: {Operator}")
            };

            return new AssertionResult(
                Id,
                Field.ToString(),
                isSuccess,
                actualValue,
                Expected,
                isSuccess
                    ? "Assertion passed"
                    : $"Assertion failed: actual={actualValue}, expected={Expected}"
            );
        }
    }
}