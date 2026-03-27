namespace AutoTest.Core.Assertion;

public interface IAssertion
{
    public Guid Id { get; }
    public Task<AssertionResult> EvaluateAsync(ExecutionResult executionResult);
}
