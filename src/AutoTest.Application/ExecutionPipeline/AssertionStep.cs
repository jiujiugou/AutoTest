using AutoTest.Application.Builder;
using AutoTest.Core.Assertion;

namespace AutoTest.Application.ExecutionPipeline;

public class AssertionStep : IPipelineStep
{
    private readonly IEnumerable<IAssertionBuilder> _builders;

    public AssertionStep(IEnumerable<IAssertionBuilder> builders)
    {
        _builders = builders;
    }

    public async Task InvokeAsync(PipelineContext context, Func<Task> next)
    {
        var executionResult = context.Result;

        if (executionResult == null)
            throw new InvalidOperationException("Execution result cannot be null.");

        //关键：Rule → Assertion
        var assertions = context.Monitor.Assertions
            .Select(rule =>
            {
                var builder = _builders
                    .Single(b => b.Type == rule.Type);

                return builder.Build(rule);
            })
            .ToList();

        // 执行断言
        executionResult.Assertions = (await Task.WhenAll(
            assertions.Select(a => a.EvaluateAsync(executionResult))
        )).ToList();

        await next();
    }
}
