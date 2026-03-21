using System;
using EventCommons;
using AutoTest.Application.Execution;
using AutoTest.Core;

public class Orchestrator
{
    private readonly ExecutionEngineResolver? resolver;
    private readonly IEventbus? eventbus;

    public Orchestrator(ExecutionEngineResolver? resolver)
    {
        this.resolver = resolver;
    }

    public async Task<ExecutionResult> ExecuteAsync(MonitorEntity monitor)
    {
        var engine = resolver.Resolve(monitor.Target);

        var result = await engine.ExecuteAsync(monitor.Target);

        return result;
    }
}
