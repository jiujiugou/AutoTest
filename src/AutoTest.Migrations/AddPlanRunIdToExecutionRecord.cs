using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026051102)]
public sealed class AddPlanRunIdToExecutionRecord : Migration
{
    public override void Up()
    {
        Alter.Table("ExecutionRecord")
            .AddColumn("PlanRunId").AsGuid().Nullable();

        Create.Index("IX_ExecutionRecord_PlanRunId")
            .OnTable("ExecutionRecord")
            .OnColumn("PlanRunId").Ascending();
    }

    public override void Down()
    {
        Delete.Index("IX_ExecutionRecord_PlanRunId").OnTable("ExecutionRecord");
        Delete.Column("PlanRunId").FromTable("ExecutionRecord");
    }
}
