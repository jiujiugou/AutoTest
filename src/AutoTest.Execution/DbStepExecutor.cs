using System.Text.Json;
using AutoTest.Core;
using AutoTest.Core.Dsl;
using AutoTest.Core.Execution;
using AutoTest.Core.Target.Db;
using AutoTest.Execution.Db;

namespace AutoTest.Execution;

internal class DbStepExecutor : IStepExecutor
{
    public string Type => "DB";
    private readonly DbExecutionEngine _engine;

    public DbStepExecutor(DbExecutionEngine engine) => _engine = engine;

    public async Task<StepResult> ExecuteAsync(JsonElement input, CancellationToken ct)
    {
        var target = JsonSerializer.Deserialize<DbTarget>(input.GetRawText(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

        var result = await _engine.ExecuteAsync(target);

        if (result is DbExecutionResult dbResult)
        {
            return new StepResult
            {
                StatusCode = dbResult.IsExecutionSuccess ? 1 : 0,
                Body = dbResult.Rows != null ? JsonSerializer.Serialize(dbResult.Rows) : null,
                ElapsedMs = dbResult.ElapsedMilliseconds,
                IsSuccess = dbResult.IsExecutionSuccess,
                ErrorMessage = dbResult.ErrorMessage
            };
        }

        return new StepResult { IsSuccess = false, ErrorMessage = result.ErrorMessage };
    }
}
