using AutoTest.Core;
using AutoTest.Core.Assertion;
using AutoTest.Execution.Http;

namespace AutoTest.Assertions.Http
{
    public class HttpAssertion : IAssertion
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        public HttpAssertionField Field { get; }
        public HttpAssertionOperator Operator { get; }
        public string Expected { get; }

        public HttpAssertion(
            HttpAssertionField field,
            HttpAssertionOperator op,
            string expected)
        {
            Field = field;
            Operator = op;
            Expected = expected;
        }

        public Task<AssertionResult> EvaluateAsync(ExecutionResult executionResult)
        {
            if (executionResult is not HttpExecutionResult httpResult)
            {
                return Task.FromResult(new AssertionResult(
                    Id,
                    Field.ToString(),
                    false,
                    null,
                    Expected,
                    "Execution result is not HttpExecutionResult"
                ));
            }

            string actualValue = Field switch
            {
                HttpAssertionField.StatusCode => httpResult.StatusCode.ToString(),
                HttpAssertionField.Body => httpResult.Body,
                _ => throw new InvalidOperationException($"Unsupported HttpAssertionField: {Field}")
            };

            bool isSuccess = Operator switch
            {
                HttpAssertionOperator.Equal => actualValue == Expected,
                HttpAssertionOperator.Contains => actualValue.Contains(Expected),
                _ => throw new InvalidOperationException($"Unsupported HttpAssertionOperator: {Operator}")
            };

            return Task.FromResult(new AssertionResult(
                Id,
                Field.ToString(),
                isSuccess,
                actualValue,
                Expected,
                isSuccess ? "Assertion passed" : "Assertion failed"
            ));
        }

        public bool CanHandle(ExecutionResult executionResult)
        {
            return executionResult is HttpExecutionResult;
        }
    }
}