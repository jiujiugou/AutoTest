using AutoTest.Application.Dto;
using AutoTest.Core;
using AutoTest.Core.Abstraction;
using AutoTest.Core.Assertion;
using AutoTest.Core.Target;
using AutoTest.Core.Target.Db;
using AutoTest.Core.Target.Http;
using AutoTest.Core.Target.Python;
using Dapper;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AutoTest.Infrastructure;

public class MonitorRepository : IMonitorRepository
{
    private readonly IDbConnection _dbConnection;
    private readonly ILogger<MonitorRepository> _logger;

    public MonitorRepository(IDbConnection dbConnection, ILogger<MonitorRepository> logger)
    {
        _dbConnection = dbConnection;
        _logger = logger;
    }

    public async Task<MonitorEntity?> GetByIdAsync(Guid id, IDbTransaction? tx = null)
    {
        try
        {
            _logger.LogDebug("Fetching monitor by Id {Id}", id);

            var sqlMonitor = @"SELECT Id, Name, Status, LastRunTime, IsEnabled, TargetType, TargetConfig
                               FROM Monitor
                               WHERE Id = @Id";

            var dto = await _dbConnection.QuerySingleOrDefaultAsync<MonitorDto>(sqlMonitor, new { Id = id }, tx);

            if (dto == null)
            {
                _logger.LogWarning("Monitor not found: {Id}", id);
                return null;
            }

            MonitorTarget target = dto.TargetType switch
            {
                "HTTP" or "Http" => JsonSerializer.Deserialize<HttpTarget>(dto.TargetConfig)!,
                
                "TCP" or "Tcp" => JsonSerializer.Deserialize<TcpTarget>(dto.TargetConfig)!,

                "Db" or "DB" => JsonSerializer.Deserialize<DbTarget>(dto.TargetConfig)!,
                _ => throw new InvalidOperationException($"Unknown TargetType: {dto.TargetType}")
            };

            var entity = new MonitorEntity(
                dto.Id,
                dto.Name,
                target,
                (MonitorStatus)dto.Status,
                dto.LastRunTime,
                dto.IsEnabled
            );

            var sqlAssertion = @"SELECT Id, Type, ConfigJson
                                 FROM Assertion
                                 WHERE MonitorId = @MonitorId";

            var assertions = await _dbConnection.QueryAsync<AssertionDto>(sqlAssertion, new { MonitorId = id }, tx);
            foreach (var a in assertions)
            {
                var rule = new AssertionRule(a.Id, a.Type, a.ConfigJson);
                entity.AddAssertion(rule);
            }

            _logger.LogDebug("Fetched monitor {Id} with {Count} assertions", id, assertions.Count());
            return entity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching monitor {Id}", id);
            throw;
        }
    }

    public async Task AddAsync(MonitorEntity monitorEntity, IDbTransaction? tx = null)
    {
        try
        {
            _logger.LogInformation("Adding monitor {Id} with name {Name}", monitorEntity.Id, monitorEntity.Name);

            var sql = @"INSERT INTO Monitor(Id, Name, Status, LastRunTime, IsEnabled, TargetType, TargetConfig)
                        VALUES(@Id, @Name, @Status, @LastRunTime, @IsEnabled, @TargetType, @TargetConfig)";

            await _dbConnection.ExecuteAsync(sql, new
            {
                monitorEntity.Id,
                monitorEntity.Name,
                Status = (int)monitorEntity.Status,
                monitorEntity.LastRunTime,
                monitorEntity.IsEnabled,
                TargetType = monitorEntity.Target.Type,
                TargetConfig = monitorEntity.Target.ToJson()
            }, tx);

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
                    assertionParams, tx);

                _logger.LogInformation("Added {Count} assertions for monitor {Id}", monitorEntity.Assertions.Count, monitorEntity.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add monitor {Id}", monitorEntity.Id);
            throw;
        }
    }

    public async Task UpdateAsync(MonitorEntity monitorEntity, IDbTransaction? tx = null)
    {
        try
        {
            _logger.LogInformation("Updating monitor {Id}", monitorEntity.Id);

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
                _logger.LogWarning("Monitor {Id} not found for update", monitorEntity.Id);
                throw new InvalidOperationException($"Monitor with Id {monitorEntity.Id} does not exist.");
            }

            await _dbConnection.ExecuteAsync("DELETE FROM Assertion WHERE MonitorId = @MonitorId",
                new { MonitorId = monitorEntity.Id }, tx);

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
                    assertionParams, tx);

                _logger.LogInformation("Updated {Count} assertions for monitor {Id}", monitorEntity.Assertions.Count, monitorEntity.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update monitor {Id}", monitorEntity.Id);
            throw;
        }
    }

    public async Task RemoveAsync(Guid id, IDbTransaction? tx = null)
    {
        try
        {
            _logger.LogInformation("Removing monitor {Id}", id);

            await _dbConnection.ExecuteAsync("DELETE FROM Assertion WHERE MonitorId = @MonitorId", new { MonitorId = id }, tx);
            await _dbConnection.ExecuteAsync("DELETE FROM Monitor WHERE Id = @Id", new { Id = id }, tx);

            _logger.LogInformation("Monitor {Id} removed", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove monitor {Id}", id);
            throw;
        }
    }

    public async Task<IEnumerable<MonitorEntity>> GetPendingTasksAsync()
    {
        try
        {
            _logger.LogDebug("Fetching schedulable monitors");

            var sql = @"SELECT Id, Name, Status, LastRunTime, IsEnabled, TargetType, TargetConfig
                        FROM Monitor
                        WHERE IsEnabled = 1 AND Status != @Running";

            var result = await _dbConnection.QueryAsync<MonitorDto>(sql, new { Running = (int)MonitorStatus.Running });
            if (!result.Any())
            {
                return Enumerable.Empty<MonitorEntity>();
            }
            var monitorIds = result.Select(r => r.Id).ToList();
            var sqlassertion = @"SELECT Id, MonitorId, Type, ConfigJson
                    FROM Assertion
                    WHERE MonitorId IN @MonitorIds";
            var assertionResults = await _dbConnection.QueryAsync<AssertionDto>(sqlassertion, new { MonitorIds = monitorIds });
            var assertionLookup = assertionResults
                .GroupBy(a => a.MonitorId)
                .ToDictionary(g => g.Key, g => g.ToList());
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true // 属性名不区分大小写
            };
            options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: true)); // 枚举值大小写不敏感

            var entities = result.Select(dto =>
            {
                try
                {
                    MonitorTarget target = dto.TargetType switch
                    {

                        "HTTP" or "Http" => JsonSerializer.Deserialize<HttpTarget>(dto.TargetConfig, options)!,
                        "TCP" or "Tcp" => JsonSerializer.Deserialize<TcpTarget>(dto.TargetConfig)!,
                        "Db" or "DB" => JsonSerializer.Deserialize<DbTarget>(dto.TargetConfig)!,
                        _ => throw new InvalidOperationException($"Unknown TargetType: {dto.TargetType}")
                    };

                    var entity = new MonitorEntity(
                        dto.Id,
                        dto.Name,
                        target,
                        (MonitorStatus)dto.Status,
                        dto.LastRunTime,
                        dto.IsEnabled
                    );
                    if (assertionLookup.TryGetValue(dto.Id, out var assertions))
                    {
                        foreach (var a in assertions)
                        {
                            entity.AddAssertion(new AssertionRule(a.Id, a.Type, a.ConfigJson));
                        }
                    }
                    return entity;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize TargetConfig for monitor {TargetConfig}", dto.TargetConfig);
                    throw new InvalidOperationException($"Invalid TargetConfig for monitor {dto.Id}", ex);
                }
    });

            _logger.LogDebug("Fetched {Count} schedulable monitors", entities.Count());
            return entities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch schedulable monitors");
            throw;
        }
    }

    public async Task<IEnumerable<MonitorEntity>> ListAsync(int take = 50)
    {
        try
        {
            var sql = @"SELECT Id, Name, Status, LastRunTime, IsEnabled, TargetType, TargetConfig
                        FROM Monitor
                        ORDER BY CreatedAt DESC
                        LIMIT @Take";

            var result = (await _dbConnection.QueryAsync<MonitorDto>(sql, new { Take = take })).ToList();
            if (result.Count == 0)
                return Enumerable.Empty<MonitorEntity>();

            var monitorIds = result.Select(r => r.Id).ToList();
            var sqlAssertion = @"SELECT Id, MonitorId, Type, ConfigJson
                                 FROM Assertion
                                 WHERE MonitorId IN @MonitorIds";

            var assertionResults = await _dbConnection.QueryAsync<AssertionDto>(sqlAssertion, new { MonitorIds = monitorIds });
            var assertionLookup = assertionResults
                .GroupBy(a => a.MonitorId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return result.Select(dto =>
            {
                MonitorTarget target = dto.TargetType switch
                {
                    "HTTP" or "Http" => JsonSerializer.Deserialize<HttpTarget>(dto.TargetConfig)!,
                    "TCP" or "Tcp" => JsonSerializer.Deserialize<TcpTarget>(dto.TargetConfig)!,
                    "Db" or "DB" => JsonSerializer.Deserialize<DbTarget>(dto.TargetConfig)!,
                    "PYTHON" or "Python" or "python" => JsonSerializer.Deserialize<PythonTarget>(dto.TargetConfig)!,
                    _ => throw new InvalidOperationException($"Unknown TargetType: {dto.TargetType}")
                };

                var entity = new MonitorEntity(
                    dto.Id,
                    dto.Name,
                    target,
                    (MonitorStatus)dto.Status,
                    dto.LastRunTime,
                    dto.IsEnabled
                );

                if (assertionLookup.TryGetValue(dto.Id, out var assertions))
                {
                    foreach (var a in assertions)
                        entity.AddAssertion(new AssertionRule(a.Id, a.Type, a.ConfigJson));
                }

                return entity;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list monitors");
            throw;
        }
    }
}
