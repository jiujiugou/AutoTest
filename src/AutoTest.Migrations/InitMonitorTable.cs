using FluentMigrator;
namespace AutoTest.Migrations;

[Migration(2026032201)]
public class InitMonitorTable : Migration
{
    public override void Up()
    {
        Create.Table("Monitor")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("Name").AsString(100).NotNullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("LastRunTime").AsDateTime().Nullable()
            .WithColumn("IsEnabled").AsBoolean().NotNullable()
            .WithColumn("TargetType").AsString(50).NotNullable()
            .WithColumn("TargetConfig").AsString(int.MaxValue).NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Index("IX_Monitor_TargetType")
            .OnTable("Monitor")
            .OnColumn("TargetType").Ascending();
    }

    public override void Down()
    {
        Delete.Table("Monitor");
    }
}
