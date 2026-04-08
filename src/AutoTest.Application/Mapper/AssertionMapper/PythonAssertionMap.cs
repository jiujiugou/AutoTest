using AutoTest.Core;
using AutoTest.Core.Assertion;

namespace AutoTest.Application.Mapper.AssertionMapper;

internal class PythonAssertionMap : IAssertionMap
{
    public string Type => "PYTHON";

    public IAssertion Map(AssertionRule rule)
    {
        return new NotSupportedPythonAssertion(rule.Id);
    }

    private sealed class NotSupportedPythonAssertion : IAssertion
    {
        public Guid Id { get; }

        public NotSupportedPythonAssertion(Guid id)
        {
            Id = id;
        }

        public Task<AssertionResult> EvaluateAsync(ExecutionResult executionResult)
        {
            return Task.FromResult(new AssertionResult(
                Id,
                "python",
                false,
                null,
                null,
                "Python assertion is not implemented"
            ));
        }
    }
}
