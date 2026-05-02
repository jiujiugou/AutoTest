using System.Text.Json;
using AutoTest.Core;
using AutoTest.Core.Dsl;
using AutoTest.Core.Execution;
using AutoTest.Core.Target;
using AutoTest.Execution.Tcp;

namespace AutoTest.Execution;

internal class TcpStepExecutor : IStepExecutor
{
    public string Type => "tcp";
    private readonly IExecutionEngine _engine;

    public TcpStepExecutor(IExecutionEngine engine) => _engine = engine;

    public async Task<StepResult> ExecuteAsync(JsonElement input, CancellationToken ct)
    {
        var target = JsonSerializer.Deserialize<TcpTarget>(input.GetRawText(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

        var result = await _engine.ExecuteAsync(target);

        if (result is TcpExecutionResult tcpResult)
        {
            return new StepResult
            {
                StatusCode = tcpResult.Connected ? 1 : 0,
                Body = tcpResult.Response,
                ElapsedMs = (long)tcpResult.LatencyMs,
                IsSuccess = tcpResult.IsExecutionSuccess,
                ErrorMessage = tcpResult.ErrorMessage
            };
        }

        return new StepResult { IsSuccess = false, ErrorMessage = result.ErrorMessage };
    }
}
