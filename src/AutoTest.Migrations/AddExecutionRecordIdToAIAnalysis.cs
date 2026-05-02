using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026042802)]
public class AddExecutionRecordIdToAIAnalysis : Migration
{
    public override void Up()
    {
        if (!Schema.Table("AIAnalysis").Column("ExecutionRecordId").Exists())
        {
            Alter.Table("AIAnalysis")
                .AddColumn("ExecutionRecordId").AsGuid().Nullable();

            Create.Index("IX_AIAnalysis_ExecutionRecordId")
                .OnTable("AIAnalysis")
                .OnColumn("ExecutionRecordId");
        }
    }

    public override void Down()
    {
        if (Schema.Table("AIAnalysis").Column("ExecutionRecordId").Exists())
        {
            Delete.Index("IX_AIAnalysis_ExecutionRecordId").OnTable("AIAnalysis");
            Delete.Column("ExecutionRecordId").FromTable("AIAnalysis");
        }
    }
}
