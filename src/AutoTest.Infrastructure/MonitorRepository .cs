using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using AutoTest.Core;
using AutoTest.Core.Abstraction;
using AutoTest.Core.Assertion;
using System.Text.Json;
using AutoTest.Core.Target.Http;
using AutoTest.Application.Dto;

namespace AutoTest.Infrastructure;

public class MonitorRepository : IMonitorRepository
{
    private readonly IDbConnection _dbConnection;

    public MonitorRepository(IDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<MonitorEntity?> GetByIdAsync(Guid id, IDbTransaction? tx = null)
    {
        var sqlMonitor = @"SELECT Id, Name, Status, LastRunTime, IsEnabled, TargetType, TargetConfig
                           FROM Monitor
                           WHERE Id = @Id";

        var dto = await _dbConnection.QuerySingleOrDefaultAsync<MonitorDto>(sqlMonitor, new { Id = id }, tx);

        if (dto == null) return null;

        // 反序列化 Target
        MonitorTarget target = dto.TargetType switch
        {
            "Http" => JsonSerializer.Deserialize<HttpTarget>(dto.TargetConfig)!,
            _ => throw new InvalidOperationException($"Unknown TargetType: {dto.TargetType}")
        };

        // 构建领域对象
        var entity = new MonitorEntity(
            dto.Id,
            dto.Name,
            target,
            (MonitorStatus)dto.Status,
            dto.LastRunTime,
            dto.IsEnabled
        );

        // 查询 Assertions
        var sqlAssertion = @"SELECT Id, Type, ConfigJson
                            FROM Assertion
                            WHERE MonitorId = @MonitorId";

        var assertions = await _dbConnection.QueryAsync<AssertionDto>(sqlAssertion, new { MonitorId = id }, tx);

        foreach (var a in assertions)
        {
            var rule = new AssertionRule(a.Id, a.Type, a.ConfigJson);
            entity.AddAssertion(rule);
        }

        return entity;
    }

    public async Task AddAsync(MonitorEntity monitorEntity, IDbTransaction? tx = null)
    {
        // 插入 Monitor
        var sql = @"INSERT INTO Monitor(Id, Name, Status, LastRunTime, IsEnabled, TargetType, TargetConfig)
                        VALUES(@Id, @Name, @Status, @LastRunTime, @Enabled, @TargetType, @TargetConfig)";

        await _dbConnection.ExecuteAsync(sql, new
        {
            monitorEntity.Id,
            monitorEntity.Name,
            monitorEntity.Status,
            monitorEntity.LastRunTime,
            monitorEntity.IsEnabled,
            TargetType = monitorEntity.Target.Type,
            TargetConfig = monitorEntity.Target.ToJson()
        }, tx);

        // 批量插入断言
        if (monitorEntity.Assertions.Any())
        {
            var assertionParams = monitorEntity.Assertions.Select(a => new
            {
                a.Id,
                MonitorId = monitorEntity.Id,
                a.Type,
                a.ConfigJson
            });
            await _dbConnection.ExecuteAsync(
                @"INSERT INTO Assertion(Id, MonitorId, Type, ConfigJson)
                      VALUES(@Id, @MonitorId, @Type, @ConfigJson)", assertionParams, tx);
        }
    }

    #region UpdateAsync
    public async Task UpdateAsync(MonitorEntity monitorEntity, IDbTransaction? tx = null)
    {
        var sql = @"UPDATE Monitor
                    SET Name = @Name,
                        Status = @Status,
                        LastRunTime = @LastRunTime,
                        IsEnabled = @IsEnabled,
                        TargetType = @TargetType,
                        TargetConfig = @TargetConfig
                    WHERE Id = @Id";

        var affected = await _dbConnection.ExecuteAsync(sql, new
        {
            monitorEntity.Name,
            Status = (int)monitorEntity.Status,
            monitorEntity.LastRunTime,
            monitorEntity.IsEnabled,
            TargetType = monitorEntity.Target.Type,
            TargetConfig = monitorEntity.Target.ToJson(),
            monitorEntity.Id
        }, tx);
        if (affected == 0)
        {
            throw new InvalidOperationException($"Monitor with Id {monitorEntity.Id} does not exist.");
        }
        // 简单策略：删掉旧 Assertion，重新插入
        await _dbConnection.ExecuteAsync(
            "DELETE FROM Assertion WHERE MonitorId = @MonitorId",
            new { MonitorId = monitorEntity.Id },
            tx);

        if (monitorEntity.Assertions.Any())
        {
            var assertionParams = monitorEntity.Assertions.Select(a => new
            {
                a.Id,
                MonitorId = monitorEntity.Id,
                a.Type,
                a.ConfigJson
            });

            await _dbConnection.ExecuteAsync(
                @"INSERT INTO Assertion(Id, MonitorId, Type, ConfigJson)
                  VALUES(@Id, @MonitorId, @Type, @ConfigJson)",
                assertionParams,
                tx);
        }
    }
    #endregion

    #region RemoveAsync
    public async Task RemoveAsync(Guid id, IDbTransaction? tx = null)
    {
        // 先删子表
        await _dbConnection.ExecuteAsync(
            "DELETE FROM Assertion WHERE MonitorId = @MonitorId",
            new { MonitorId = id },
            tx);

        // 再删主表
        await _dbConnection.ExecuteAsync(
            "DELETE FROM Monitor WHERE Id = @Id",
            new { Id = id },
            tx);
    }
    #endregion
}