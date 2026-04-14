using AutoTest.Core;
using AutoTest.Core.Assertion;

namespace AutoTest.Application;

/// <summary>
/// 断言执行器：对一组断言并发评估并汇总结果。
/// </summary>
public class AssertionEngine
{
    /// <summary>
    /// 评估断言集合并返回断言结果列表。
    /// </summary>
    /// <param name="result">执行结果（作为断言上下文）。</param>
    /// <param name="assertions">要评估的断言集合。</param>
    /// <returns>断言结果列表。</returns>
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
