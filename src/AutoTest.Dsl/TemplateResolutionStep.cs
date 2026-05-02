using System.Text.Json;
using AutoTest.Core.Dsl;
using AutoTest.Core.ExecutionPipeline;

namespace AutoTest.Dsl;

public class TemplateResolutionStep : IPipelineStep
{
    private readonly IDslParser _parser;

    public TemplateResolutionStep(IDslParser parser)
    {
        _parser = parser;
    }

    public async Task InvokeAsync(PipelineContext context, Func<Task> next)
    {
        if (!context.Monitor.IsTemplate)
        {
            await next();
            return;
        }

        var variables = JsonSerializer.Deserialize<Dictionary<string, string>>(
            context.Monitor.TemplateVariablesJson ?? "{}")!;

        var dag = await _parser.ParseAsync(
            context.Monitor.Target.ToJson(), variables);

        context.Items[typeof(DslPipelineContext).FullName!] = new DslPipelineContext
        {
            Dag = dag,
            Variables = variables
        };

        await next();
    }
}
