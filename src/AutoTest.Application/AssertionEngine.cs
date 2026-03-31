using AutoTest.Core;
using AutoTest.Core.Assertion;

namespace AutoTest.Application;

public class AssertionEngine
{
    public Task<List<AssertionResult>> EvaluateAsync(
    ExecutionResult result,
    IEnumerable<IAssertion> assertions)
    {
        var results = assertions.Select(a =>
        {
            try
            {
                return a.EvaluateAsync(result).Result; // 阻塞获取 Task 结果
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

        return Task.FromResult(results.ToList());
    }
}
