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
    private readonly PythonExecutionEngine _engine;

    public PythonStepExecutor(PythonExecutionEngine engine) => _engine = engine;

    public async Task<StepResult> ExecuteAsync(JsonElement input, CancellationToken ct)
    {
        var target = JsonSerializer.Deserialize<PythonTarget>(input.GetRawText(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

        var result = await _engine.ExecuteAsync(target);
        var pyResult = result as PythonExecutionResult;
        return new StepResult
        {
            StatusCode = result.IsExecutionSuccess ? (pyResult?.ExitCode ?? 0) : 0,
            Body = pyResult?.StdOut ?? result.ErrorMessage,
            ElapsedMs = pyResult?.ElapsedMs ?? 0,
            IsSuccess = result.IsExecutionSuccess,
            ErrorMessage = result.ErrorMessage
        };
    }
}
