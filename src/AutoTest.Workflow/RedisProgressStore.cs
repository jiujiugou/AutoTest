using System.Text.Json;
using AutoTest.Core.Dsl;
using StackExchange.Redis;

namespace AutoTest.Workflow;

internal class RedisProgressStore : IProgressStore
{
    private readonly IDatabase _db;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public RedisProgressStore(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    private static string Key(string executionId)
        => $"dsl:progress:{executionId}";

    public async Task SaveAsync(DslRuntimeContext ctx)
    {
        var key = Key(ctx.ExecutionId);
        var payload = JsonSerializer.Serialize(ctx, Options);
        await _db.StringSetAsync(key, payload, TimeSpan.FromHours(6), When.Always, CommandFlags.FireAndForget);
    }

    public async Task<DslRuntimeContext?> RestoreAsync(string executionId)
    {
        var key = Key(executionId);
        var value = await _db.StringGetAsync(key);
        if (value.IsNullOrEmpty) return null;
        return JsonSerializer.Deserialize<DslRuntimeContext>(value.ToString()!);
    }

    public async Task CompleteAsync(string executionId)
    {
        await _db.KeyDeleteAsync(Key(executionId));
    }
}
