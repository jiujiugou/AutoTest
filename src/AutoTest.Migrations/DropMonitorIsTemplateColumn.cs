using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026050901)]
public class DropMonitorIsTemplateColumn : Migration
{
    public override void Up()
    {
        if (Schema.Table("Monitor").Column("IsTemplate").Exists())
            Delete.Column("IsTemplate").FromTable("Monitor");
    }

    public override void Down()
    {
        if (!Schema.Table("Monitor").Column("IsTemplate").Exists())
            Alter.Table("Monitor")
                .AddColumn("IsTemplate")
                .AsBoolean()
                .WithDefaultValue(false);
    }
}
