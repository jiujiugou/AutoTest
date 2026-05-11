using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026051101)]
public sealed class CreateTestPlanTable : Migration
{
    public override void Up()
    {
        Create.Table("TestPlan")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("Description").AsString(2000).Nullable()
            .WithColumn("MonitorIdsJson").AsString(int.MaxValue).NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("UpdatedAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Index("IX_TestPlan_CreatedAt")
            .OnTable("TestPlan")
            .OnColumn("CreatedAt").Descending();
    }

    public override void Down()
    {
        Delete.Index("IX_TestPlan_CreatedAt").OnTable("TestPlan");
        Delete.Table("TestPlan");
    }
}
