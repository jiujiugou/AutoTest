using AutoTest.Core.AI;
using AutoTest.Core.Repositories;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace AutoTest.Infrastructure
{
    internal class AnalysisRepository : IAnalysisRepository
    {
        private readonly IDbConnection _connection;

        public AnalysisRepository(IDbConnection connection)
        {
            _connection = connection;
        }

        public async Task AddAsync(AIAnalysis analysis)
        {
            const string sql = """
    INSERT INTO AIAnalysis
    (Id, OutboxMessageId, ExecutionRecordId, Type, Severity, Category, RootCause, Suggestion, Summary,
     Confidence, InputJson, OutputJson, Model, PromptVersion, CreatedAt, ProcessedAt)
    VALUES
    (@Id, @OutboxMessageId, @ExecutionRecordId, @Type, @Severity, @Category, @RootCause, @Suggestion, @Summary,
     @Confidence, @InputJson, @OutputJson, @Model, @PromptVersion, @CreatedAt, @ProcessedAt)
    """;

            await _connection.ExecuteAsync(sql, analysis);
        }

        public async Task<AIAnalysis?> GetByExecutionRecordIdAsync(Guid executionRecordId)
        {
            const string sql = """
        SELECT Id, OutboxMessageId, ExecutionRecordId, Type, Severity, Category,
               RootCause, Suggestion, Summary, Confidence, Model, PromptVersion,
               CreatedAt, ProcessedAt
        FROM AIAnalysis
        WHERE ExecutionRecordId = @ExecutionRecordId
        """;
            return await _connection.QuerySingleOrDefaultAsync<AIAnalysis>(
                sql, new { ExecutionRecordId = executionRecordId });
        }

        public async Task<List<AIAnalysis>> GetByMonitorIdAsync(Guid monitorId, int take = 20)
        {
            const string sql = """
        SELECT a.Id, a.OutboxMessageId, a.ExecutionRecordId, a.Type, a.Severity, a.Category,
               a.RootCause, a.Suggestion, a.Summary, a.Confidence, a.Model, a.PromptVersion,
               a.CreatedAt, a.ProcessedAt
        FROM AIAnalysis a
        INNER JOIN ExecutionRecord e ON e.Id = a.ExecutionRecordId
        WHERE e.MonitorId = @MonitorId
        ORDER BY a.CreatedAt DESC
        OFFSET 0 ROWS FETCH NEXT @Take ROWS ONLY
        """;
            var rows = await _connection.QueryAsync<AIAnalysis>(
                sql, new { MonitorId = monitorId, Take = take });
            return rows.ToList();
        }
    }
}
