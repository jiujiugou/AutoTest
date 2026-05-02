using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026042701)]
public sealed class CreateAiTaskTable : Migration
{
    public override void Up()
    {
        Create.Table("AiTask")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("TaskType").AsString(100).NotNullable()
            .WithColumn("BizId").AsGuid().Nullable()
            .WithColumn("InputJson").AsString(int.MaxValue).NotNullable()
            .WithColumn("OutputJson").AsString(int.MaxValue).Nullable()
            .WithColumn("Attempts").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("Status").AsString(50).NotNullable()
            .WithColumn("NextRunAt").AsDateTime().NotNullable()
            .WithColumn("LockedBy").AsString(200).Nullable()
            .WithColumn("LockedAt").AsDateTime().Nullable()
            .WithColumn("Error").AsString(int.MaxValue).Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Create.Index("IX_AiTask_Status_NextRunAt")
            .OnTable("AiTask")
            .OnColumn("Status").Ascending()
            .OnColumn("NextRunAt").Ascending();

        Create.Index("IX_AiTask_BizId")
            .OnTable("AiTask")
            .OnColumn("BizId").Ascending();
    }

    public override void Down()
    {
        Delete.Table("AiTask");
    }
}
