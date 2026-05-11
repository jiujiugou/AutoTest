using AutoTest.Core.Dsl;

namespace AutoTest.Execution;

/// <summary>
/// 从 DI 容器收集所有 <see cref="IStepExecutor"/> 实现，按 Type 索引查找。
/// </summary>
internal class StepExecutorResolver : IStepExecutorResolver
{
    private readonly Dictionary<string, IStepExecutor> _executors;

    public StepExecutorResolver(IEnumerable<IStepExecutor> executors)
    {
        _executors = executors.ToDictionary(e => e.Type, StringComparer.OrdinalIgnoreCase);
    }

    public IStepExecutor Resolve(string type)
    {
        if (_executors.TryGetValue(type, out var executor))
            return executor;
        throw new InvalidOperationException($"不支持的步骤执行器类型: '{type}'");
    }
}
