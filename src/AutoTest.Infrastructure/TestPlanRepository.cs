using System.Data;
using System.Text.Json;
using AutoTest.Core;
using AutoTest.Core.Repositories;
using Dapper;
using Microsoft.Extensions.Logging;

namespace AutoTest.Infrastructure;

public class TestPlanRepository : ITestPlanRepository
{
    private readonly IDbConnection _dbConnection;
    private readonly ILogger<TestPlanRepository> _logger;

    public TestPlanRepository(IDbConnection dbConnection, ILogger<TestPlanRepository> logger)
    {
        _dbConnection = dbConnection;
        _logger = logger;
    }

    public async Task<TestPlanEntity?> GetByIdAsync(Guid id, IDbTransaction? tx = null)
    {
        const string sql = """
                           SELECT Id, Name, Description, MonitorIdsJson, CreatedAt, UpdatedAt
                           FROM TestPlan
                           WHERE Id = @Id
                           """;

        var row = await _dbConnection.QuerySingleOrDefaultAsync<TestPlanRow>(sql, new { Id = id }, tx);
        return row == null ? null : Map(row);
    }

    public async Task<IEnumerable<TestPlanEntity>> ListAsync(int take = 50)
    {
        var isSqlServer = _dbConnection is Microsoft.Data.SqlClient.SqlConnection;
        var sql = isSqlServer
            ? """
              SELECT Id, Name, Description, MonitorIdsJson, CreatedAt, UpdatedAt
              FROM TestPlan
              ORDER BY CreatedAt DESC
              OFFSET 0 ROWS FETCH NEXT @Take ROWS ONLY
              """
            : """
              SELECT Id, Name, Description, MonitorIdsJson, CreatedAt, UpdatedAt
              FROM TestPlan
              ORDER BY CreatedAt DESC
              LIMIT @Take
              """;

        var rows = await _dbConnection.QueryAsync<TestPlanRow>(sql, new { Take = take });
        return rows.Select(Map).ToList();
    }

    public Task AddAsync(TestPlanEntity plan, IDbTransaction? tx = null)
    {
        const string sql = """
                           INSERT INTO TestPlan(Id, Name, Description, MonitorIdsJson, CreatedAt, UpdatedAt)
                           VALUES(@Id, @Name, @Description, @MonitorIdsJson, @CreatedAt, @UpdatedAt)
                           """;

        return _dbConnection.ExecuteAsync(sql, new
        {
            plan.Id,
            plan.Name,
            plan.Description,
            plan.MonitorIdsJson,
            plan.CreatedAt,
            plan.UpdatedAt
        }, tx);
    }

    public Task UpdateAsync(TestPlanEntity plan, IDbTransaction? tx = null)
    {
        const string sql = """
                           UPDATE TestPlan
                           SET Name = @Name,
                               Description = @Description,
                               MonitorIdsJson = @MonitorIdsJson,
                               UpdatedAt = @UpdatedAt
                           WHERE Id = @Id
                           """;

        return _dbConnection.ExecuteAsync(sql, new
        {
            plan.Name,
            plan.Description,
            plan.MonitorIdsJson,
            UpdatedAt = DateTime.UtcNow,
            plan.Id
        }, tx);
    }

    public Task RemoveAsync(Guid id, IDbTransaction? tx = null)
    {
        return _dbConnection.ExecuteAsync(
            "DELETE FROM TestPlan WHERE Id = @Id", new { Id = id }, tx);
    }

    private static TestPlanEntity Map(TestPlanRow row)
    {
        var monitorIds = string.IsNullOrWhiteSpace(row.MonitorIdsJson)
            ? new List<Guid>()
            : JsonSerializer.Deserialize<List<Guid>>(row.MonitorIdsJson) ?? new List<Guid>();

        return new TestPlanEntity(
            row.Id,
            row.Name,
            row.Description,
            monitorIds,
            row.CreatedAt,
            row.UpdatedAt);
    }

    private sealed class TestPlanRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string MonitorIdsJson { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
