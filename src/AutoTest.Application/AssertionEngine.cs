using AutoTest.Core;
using AutoTest.Core.Assertion;

namespace AutoTest.Application;

public class AssertionEngine
{
    public async Task<List<AssertionResult>> EvaluateAsync(
        ExecutionResult result,
        IEnumerable<IAssertion> assertions)
    {
        var tasks = assertions
            .Where(a => a.CanHandle(result))
            .Select(a => a.EvaluateAsync(result));

        var results = await Task.WhenAll(tasks);

        return results.ToList();
    }
}
