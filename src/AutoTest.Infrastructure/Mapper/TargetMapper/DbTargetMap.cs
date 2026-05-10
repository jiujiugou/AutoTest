using System.Text.Json;
using AutoTest.Application;
using AutoTest.Application.Dto;
using AutoTest.Core;
using AutoTest.Core.Target.Db;

namespace AutoTest.Infrastructure.Mapper.TargetMapper;

public sealed class DbTargetMap : ITargetMap
{
    public string Type => "DB";

    public MonitorTarget Map(string json)
    {
        var dto = JsonSerializer.Deserialize<DbTargetDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

        var commandType = Enum.TryParse<SqlCommandType>(dto.CommandType, true, out var ct)
            ? ct : SqlCommandType.Query;

        return new DbTarget(
            connectionString: dto.ConnectionString,
            sql: dto.Sql,
            dbType: dto.DbType,
            timeoutSeconds: dto.TimeoutSeconds > 0 ? dto.TimeoutSeconds : 30,
            commandType: commandType,
            enableRetry: dto.EnableRetry,
            retryCount: dto.RetryCount,
            retryDelayMs: dto.RetryDelayMs
        );
    }
}
