namespace AutoTest.Core.Assertion;

public interface IAssertion
{
    public Guid Id { get; }
    bool CanHandle(ExecutionResult executionResult);
    Task<AssertionResult> EvaluateAsync(ExecutionResult executionResult);
}
