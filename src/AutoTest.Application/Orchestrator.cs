using System;
using AutoTest.Application.Execution;
using AutoTest.Core;
using AutoTest.Core.Execution;
using AutoTest.Core.Abstraction;

public class Orchestrator : IOrchestrator
{
    private readonly ExecutionEngineResolver resolver;
    private readonly IMonitorRepository repository;

    public Orchestrator(
        ExecutionEngineResolver resolver,
        IMonitorRepository repository)
    {
        this.resolver = resolver;
        this.repository = repository;
    }

    public async Task<ExecutionResult> TryExecuteAsync(MonitorEntity monitor)
    {
        // 1️⃣ 判定是否执行
        if (!monitor.CanExecute())
            return null;
        monitor.MarkRunning();
        await repository.UpdateAsync(monitor);

        try
        {
            var engine = resolver.Resolve(monitor.Target);
            var result = await engine.ExecuteAsync(monitor.Target);

            if (result.IsExecutionSuccess)
            {
                monitor.MarkSuccess();
            }
            else
            {
                monitor.MarkFailed();
            }
            await repository.UpdateAsync(monitor);

            return result;
        }
        catch (Exception)
        {
            monitor.MarkFailed();
            await repository.UpdateAsync(monitor);
            throw;
        }
    }

    public async Task<IEnumerable<ExecutionResult>> TryExecuteAllAsync(IEnumerable<MonitorEntity> monitors)
    {
        var results = new List<ExecutionResult>();

        foreach (var monitor in monitors)
        {
            var result = await TryExecuteAsync(monitor);
            if (result != null)
                results.Add(result);
        }

        return results;
    }
}