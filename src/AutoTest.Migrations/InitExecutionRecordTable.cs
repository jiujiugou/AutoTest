using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026032204)]
public class InitExecutionRecordTable : Migration
{
    public override void Up()
    {
        Create.Table("ExecutionRecord")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("MonitorId").AsGuid().NotNullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("StartedAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("FinishedAt").AsDateTime().Nullable()
            .WithColumn("IsExecutionSuccess").AsBoolean().NotNullable()
            .WithColumn("ErrorMessage").AsString(int.MaxValue).Nullable()
            .WithColumn("ResultType").AsString(50).NotNullable()
            .WithColumn("ResultJson").AsString(int.MaxValue).NotNullable();

        Create.Index("IX_ExecutionRecord_MonitorId_StartedAt")
            .OnTable("ExecutionRecord")
            .OnColumn("MonitorId").Ascending()
            .OnColumn("StartedAt").Descending();

        Alter.Table("AssertionResult")
            .AddColumn("ExecutionId").AsGuid().Nullable();

        Create.Index("IX_AssertionResult_ExecutionId")
            .OnTable("AssertionResult")
            .OnColumn("ExecutionId").Ascending();
    }

    public override void Down()
    {
        Delete.Index("IX_AssertionResult_ExecutionId").OnTable("AssertionResult");
        Delete.Column("ExecutionId").FromTable("AssertionResult");

        Delete.Index("IX_ExecutionRecord_MonitorId_StartedAt").OnTable("ExecutionRecord");
        Delete.Table("ExecutionRecord");
    }
}

