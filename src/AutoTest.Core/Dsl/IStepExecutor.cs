using System.Text.Json;

namespace AutoTest.Core.Dsl;

public interface IStepExecutor
{
    string Type { get; }
    Task<StepResult> ExecuteAsync(JsonElement input, CancellationToken ct = default);
}

public class StepResult
{
    public int StatusCode { get; init; }
    public string? Body { get; init; }
    public Dictionary<string, string[]>? Headers { get; init; }
    public long ElapsedMs { get; init; }
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
}
