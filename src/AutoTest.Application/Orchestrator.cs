using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoTest.Core;
using AutoTest.Core.Execution;
using AutoTest.Core.Abstraction;
using AutoTest.Application.ExecutionPipeline;

public class Orchestrator : IOrchestrator
{
    private readonly IPipeline _pipeline;

    public Orchestrator(IPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    /// <summary>
    /// 尝试执行单个任务
    /// </summary>
    public async Task<ExecutionResult?> TryExecuteAsync(MonitorEntity monitor)
    {
        if (!monitor.CanExecute())
            return null;

        // 创建任务上下文
        var context = new PipelineContext(monitor);

        try
        {

            await _pipeline.ExecuteAsync(context);

            // 返回最终执行结果
            return context.Result ?? throw new InvalidOperationException("Pipeline did not produce a result");
        }
        catch
        {
            // Orchestrator 不负责数据库或事件，异常直接抛出
            throw;
        }
    }

}