using AutoTest.Application.ExecutionPipeline;
using AutoTest.Core.Execution;

namespace AutoTest.Application.Step;

public class ExecutionStep : IPipelineStep
{
    private readonly IExecutionEngine _execution;
    public ExecutionStep(IExecutionEngine execution)
    {
        _execution = execution;
    }

    public async Task InvokeAsync(PipelineContext context, Func<Task> next)
    {
        try
        {
            await _execution.ExecuteAsync(context.Monitor.Target);
        }
        catch (Exception ex)
        {
            //log
        }
        await next();
    }


}
