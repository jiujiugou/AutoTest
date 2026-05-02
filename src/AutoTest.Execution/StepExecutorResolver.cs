using AutoTest.Core.Dsl;

namespace AutoTest.Execution;

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
