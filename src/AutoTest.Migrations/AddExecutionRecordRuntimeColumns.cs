using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026041401)]
public sealed class AddExecutionRecordRuntimeColumns : Migration
{
    public override void Up()
    {
        Alter.Table("ExecutionRecord")
            .AddColumn("IdempotencyKey").AsString(200).Nullable()
            .AddColumn("LockedBy").AsString(200).Nullable()
            .AddColumn("HeartbeatAtUtc").AsDateTime().Nullable();

        IfDatabase("SqlServer").Execute.Sql("""
                                            IF NOT EXISTS (
                                                SELECT 1
                                                FROM sys.indexes
                                                WHERE name = N'IX_ExecutionRecord_IdempotencyKey'
                                                  AND object_id = OBJECT_ID(N'dbo.ExecutionRecord')
                                            )
                                            BEGIN
                                                CREATE UNIQUE INDEX [IX_ExecutionRecord_IdempotencyKey]
                                                ON [dbo].[ExecutionRecord] ([IdempotencyKey] ASC)
                                                WHERE [IdempotencyKey] IS NOT NULL;
                                            END
                                            """);

        IfDatabase("SQLite").Create.Index("IX_ExecutionRecord_IdempotencyKey")
            .OnTable("ExecutionRecord")
            .OnColumn("IdempotencyKey").Ascending()
            .WithOptions().Unique();

        Create.Index("IX_ExecutionRecord_Status_HeartbeatAtUtc")
            .OnTable("ExecutionRecord")
            .OnColumn("Status").Ascending()
            .OnColumn("HeartbeatAtUtc").Descending();
    }

    public override void Down()
    {
        Delete.Index("IX_ExecutionRecord_Status_HeartbeatAtUtc").OnTable("ExecutionRecord");
        Delete.Index("IX_ExecutionRecord_IdempotencyKey").OnTable("ExecutionRecord");

        Delete.Column("HeartbeatAtUtc").FromTable("ExecutionRecord");
        Delete.Column("LockedBy").FromTable("ExecutionRecord");
        Delete.Column("IdempotencyKey").FromTable("ExecutionRecord");
    }
}
