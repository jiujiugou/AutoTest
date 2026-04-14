using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026041301)]
public class AddMonitorScheduleColumns : Migration
{
    public override void Up()
    {
        Alter.Table("Monitor")
            .AddColumn("AutoDailyEnabled").AsBoolean().NotNullable().WithDefaultValue(false)
            .AddColumn("AutoDailyTime").AsString(5).Nullable()
            .AddColumn("MaxRuns").AsInt32().Nullable()
            .AddColumn("ExecutedCount").AsInt32().NotNullable().WithDefaultValue(0);
    }

    public override void Down()
    {
        Delete.Column("ExecutedCount").FromTable("Monitor");
        Delete.Column("MaxRuns").FromTable("Monitor");
        Delete.Column("AutoDailyTime").FromTable("Monitor");
        Delete.Column("AutoDailyEnabled").FromTable("Monitor");
    }
}

