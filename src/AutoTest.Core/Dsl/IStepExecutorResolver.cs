namespace AutoTest.Core.Dsl;

/// <summary>
/// 按步骤 type 查找对应的 <see cref="IStepExecutor"/>。
/// </summary>
public interface IStepExecutorResolver
{
    /// <exception cref="InvalidOperationException">type 未注册时抛出。</exception>
    IStepExecutor Resolve(string type);
}
