using AutoTest.Core;
using AutoTest.Core.Assertion;

namespace AutoTest.Application;

public class AssertionEngine
{
    public async Task<List<AssertionResult>> EvaluateAsync(
        ExecutionResult result,
        IEnumerable<IAssertion> assertions)
    {
        var tasks = assertions.Select(async a =>
        {
            try
            {
                return await a.EvaluateAsync(result);
            }
            catch (Exception ex)
            {
                return new AssertionResult(
                    a.Id,
                    "Error",
                    false,
                    null,
                    null,
                    ex.Message
                );
            }
        });
        return (await Task.WhenAll(tasks)).ToList();
    }
}
