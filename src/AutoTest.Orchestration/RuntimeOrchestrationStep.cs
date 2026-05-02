using AutoTest.Core.Dsl;
using AutoTest.Core.ExecutionPipeline;

namespace AutoTest.Orchestration;

public class RuntimeOrchestrationStep : IPipelineStep
{
    private readonly ExecutionEngine _engine;
    private static readonly string CtxKey = typeof(DslPipelineContext).FullName!;

    public RuntimeOrchestrationStep(ExecutionEngine engine)
    {
        _engine = engine;
    }

    public async Task InvokeAsync(PipelineContext context, Func<Task> next)
    {
        if (!context.Monitor.IsTemplate)
        {
            await next();
            return;
        }

        if (context.Items[CtxKey] is not DslPipelineContext dslCtx)
        {
            await next();
            return;
        }

        dslCtx.Dag.Id = context.Monitor.Id.ToString("N");

        var execCtx = await _engine.ExecuteAsync(dslCtx.Dag, dslCtx.Variables);

        var dslResult = new DslExecutionResult
        {
            Steps = execCtx.CompletedSteps,
            FinalVariables = execCtx.Variables,
            AllStepsPassed = !execCtx.IsTerminated
        };

        context.Items[typeof(DslExecutionResult).FullName!] = dslResult;

        var finalStep = execCtx.CompletedSteps.LastOrDefault();
        context.Result = new Core.Dsl.DslExecutionResultWrapper(
            finalStep?.IsSuccess ?? false,
            finalStep?.ErrorMessage)
        {
            Assertions = new List<Core.Assertion.AssertionResult>()
        };

        await next();
    }
}
