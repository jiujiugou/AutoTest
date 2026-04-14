using AutoTest.Core;
using AutoTest.Core.Assertion;
using AutoTest.Core.Execution;
using Microsoft.Extensions.Logging;

namespace AutoTest.Assertions.Tcp;

public class TcpAssertion : IAssertion
{
    public Guid Id { get; init; }
    public TcpAssertionField Field { get; }
    public TcpAssertionOperator Operator { get; }
    public string Expected { get; }
    public ILogger<TcpAssertion>? Logger { get; }
    public TcpAssertion(Guid id, TcpAssertionField field, TcpAssertionOperator op, string expected, ILogger<TcpAssertion>? logger = null)
    {
        Id = id;
        Field = field;
        Operator = op;

        Expected = expected;
        Logger = logger;
    }

    public Task<AssertionResult> EvaluateAsync(ExecutionResult executionResult)
    {
        if (executionResult is not ITcpExecutionResult tcpResult)
        {
            Logger?.LogError("Execution result is not TcpExecutionResult");
            return Task.FromResult(new AssertionResult(
                Id,
                Field.ToString(),
                false,
                null,
                null,
                "Execution result is not TcpExecutionResult"
            ));
        }

        string? actualValue = Field switch
        {
            TcpAssertionField.Connected => tcpResult.Connected.ToString(),
            TcpAssertionField.Response => tcpResult.Response,
            TcpAssertionField.LatencyMs => tcpResult.LatencyMs.ToString(),
            TcpAssertionField.SequenceCorrect => tcpResult.SequenceCorrect.ToString(),
            _ => throw new InvalidOperationException($"Unsupported TcpAssertionField: {Field}")
        };

        bool isSuccess = Operator switch
        {
            TcpAssertionOperator.Equal => actualValue == Expected,
            TcpAssertionOperator.Contains => actualValue?.Contains(Expected) == true,
            TcpAssertionOperator.LessThan => double.TryParse(actualValue, out var actualNum) && double.TryParse(Expected, out var expectedNum) && actualNum < expectedNum,
            TcpAssertionOperator.GreaterThan => double.TryParse(actualValue, out var actualNum2) && double.TryParse(Expected, out var expectedNum2) && actualNum2 > expectedNum2,
            _ => throw new InvalidOperationException($"Unsupported TcpAssertionOperator: {Operator}")
        };

        if (isSuccess)
            Logger?.LogInformation("Assertion passed: {Field} {Operator} {Expected}", Field, Operator, Expected);
        else
            Logger?.LogWarning("Assertion failed: actual={Actual}, expected={Expected}", actualValue, Expected);

        return Task.FromResult(new AssertionResult(
            Id,
            Field.ToString(),
            isSuccess,
            actualValue,
            Expected,
            null
        ));
    }
}
