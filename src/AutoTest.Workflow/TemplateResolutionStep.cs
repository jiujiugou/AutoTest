using System.Text.Json;
using AutoTest.Core.Dsl;
using AutoTest.Core.ExecutionPipeline;

namespace AutoTest.Workflow;

/// <summary>
/// 模板解析步骤：将 DSL JSON 模板解析为 DAG，写入 PipelineContext。
/// 仅在模板 Pipeline 中注册，不对非模板场景产生任何影响。
/// </summary>
public class TemplateResolutionStep : IPipelineStep
{
    private readonly IDslParser _parser;

    public TemplateResolutionStep(IDslParser parser)
    {
        _parser = parser;
    }

    public async Task InvokeAsync(PipelineContext context, Func<Task> next)
    {
        var variables = JsonSerializer.Deserialize<Dictionary<string, string>>(
            context.Monitor.TemplateVariablesJson ?? "{}")!;

        var dag = await _parser.ParseAsync(
            context.Monitor.Target.ToJson(), variables);

        dag.Id = context.Monitor.Id.ToString("N");

        context.Items[typeof(DslPipelineContext).FullName!] = new DslPipelineContext
        {
            Dag = dag,
            Variables = variables
        };

        await next();
    }
}
