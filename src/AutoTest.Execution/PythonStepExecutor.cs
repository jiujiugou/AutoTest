using System.Text.Json;
using AutoTest.Core;
using AutoTest.Core.Dsl;
using AutoTest.Core.Execution;
using AutoTest.Core.Target.Python;
using AutoTest.Execution.Python;

namespace AutoTest.Execution;

internal class PythonStepExecutor : IStepExecutor
{
    public string Type => "python";
    private readonly IExecutionEngine _engine;

    public PythonStepExecutor(IExecutionEngine engine) => _engine = engine;

    public async Task<StepResult> ExecuteAsync(JsonElement input, CancellationToken ct)
    {
        var target = JsonSerializer.Deserialize<PythonTarget>(input.GetRawText(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

        var result = await _engine.ExecuteAsync(target);
        return new StepResult
        {
            StatusCode = result.IsExecutionSuccess ? 1 : 0,
            Body = result.ErrorMessage,
            ElapsedMs = 0,
            IsSuccess = result.IsExecutionSuccess,
            ErrorMessage = result.ErrorMessage
        };
    }
}
