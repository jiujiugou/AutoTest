using System.Text.Json;
using AutoTest.Core;
using AutoTest.Core.Dsl;
using AutoTest.Core.Execution;
using AutoTest.Core.Target.Http;
using AutoTest.Execution.Http;

namespace AutoTest.Execution;

internal class HttpStepExecutor : IStepExecutor
{
    public string Type => "http";
    private readonly IExecutionEngine _engine;

    public HttpStepExecutor(IExecutionEngine engine) => _engine = engine;

    public async Task<StepResult> ExecuteAsync(JsonElement input, CancellationToken ct)
    {
        var target = JsonSerializer.Deserialize<HttpTarget>(input.GetRawText(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

        var result = await _engine.ExecuteAsync(target);

        if (result is HttpExecutionResult httpResult)
        {
            return new StepResult
            {
                StatusCode = httpResult.StatusCode,
                Body = httpResult.Body,
                Headers = httpResult.Headers?.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()),
                ElapsedMs = httpResult.ElapsedMilliseconds ?? 0,
                IsSuccess = httpResult.IsExecutionSuccess,
                ErrorMessage = httpResult.ErrorMessage
            };
        }

        return new StepResult { IsSuccess = false, ErrorMessage = result.ErrorMessage };
    }
}
