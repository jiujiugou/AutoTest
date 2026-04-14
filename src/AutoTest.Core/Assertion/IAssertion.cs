namespace AutoTest.Core.Assertion;

/// <summary>
/// 断言：对一次 <see cref="ExecutionResult"/> 进行判定，生成结构化的 <see cref="AssertionResult"/>。
/// </summary>
public interface IAssertion
{
    /// <summary>
    /// 断言实例 ID。
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// 对执行结果进行断言评估。
    /// </summary>
    /// <param name="executionResult">执行结果（可能是派生类型）。</param>
    /// <returns>断言结果。</returns>
    public Task<AssertionResult> EvaluateAsync(ExecutionResult executionResult);
}
