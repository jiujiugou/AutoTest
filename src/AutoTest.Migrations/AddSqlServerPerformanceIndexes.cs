using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026041201)]
public sealed class AddSqlServerPerformanceIndexes : Migration
{
    public override void Up()
    {
        Create.Index("IX_Monitor_CreatedAt")
            .OnTable("Monitor")
            .OnColumn("CreatedAt").Descending();

        Create.Index("IX_AssertionResult_ExecutionId_Timestamp")
            .OnTable("AssertionResult")
            .OnColumn("ExecutionId").Ascending()
            .OnColumn("Timestamp").Ascending();

        Create.Index("IX_OutboxMessage_Status_NextAttemptAt_LockedUntil_OccurredAt")
            .OnTable("OutboxMessage")
            .OnColumn("Status").Ascending()
            .OnColumn("NextAttemptAt").Ascending()
            .OnColumn("LockedUntil").Ascending()
            .OnColumn("OccurredAt").Ascending();
    }

    public override void Down()
    {
        Delete.Index("IX_OutboxMessage_Status_NextAttemptAt_LockedUntil_OccurredAt").OnTable("OutboxMessage");
        Delete.Index("IX_AssertionResult_ExecutionId_Timestamp").OnTable("AssertionResult");
        Delete.Index("IX_Monitor_CreatedAt").OnTable("Monitor");
    }
}

