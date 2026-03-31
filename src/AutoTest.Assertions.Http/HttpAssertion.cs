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
        private readonly ILogger<HttpAssertion> _logger;
        public HttpAssertion(Guid id, HttpAssertionField field, HttpAssertionOperator op, string expected, ILogger<HttpAssertion> logger = null!)
        {
            Id = id;
            Field = field;
            Operator = op;
            Expected = expected;
            _logger = logger;
        }

        public Task<AssertionResult> EvaluateAsync(ExecutionResult executionResult)
        {
            if (executionResult is not IHttpExecutionResult httpResult)
            {
                _logger.LogError("Execution result is not HttpExecutionResult");
                return Task.FromResult(new AssertionResult(
                    Id,
                    Field.ToString(),
                    false,
                    null,
                    Expected,
                    "Execution result is not HttpExecutionResult"
                ));
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

            if (isSuccess)
                _logger.LogInformation($"Assertion passed: {Field} {Operator} {Expected}");
            else
                _logger.LogWarning($"Assertion failed: actual={actualValue}, expected={Expected}");

            return Task.FromResult(new AssertionResult(
                Id,
                Field.ToString(),
                isSuccess,
                actualValue,
                Expected,
                isSuccess ? "Assertion passed" : $"Assertion failed: actual={actualValue}, expected={Expected}"
            ));
        }
    }
}