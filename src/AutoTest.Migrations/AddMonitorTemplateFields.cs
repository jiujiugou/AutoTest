using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026042801)]
public class AddMonitorTemplateFields : Migration
{
    public override void Up()
    {
        if (!Schema.Table("Monitor").Column("IsTemplate").Exists())
        {
            Alter.Table("Monitor")
                .AddColumn("IsTemplate")
                    .AsBoolean()
                    .WithDefaultValue(false)
                .AddColumn("TemplateVariablesJson")
                    .AsString(int.MaxValue)
                    .Nullable();
        }
    }

    public override void Down()
    {
        if (Schema.Table("Monitor").Column("IsTemplate").Exists())
        {
            Delete.Column("IsTemplate").FromTable("Monitor");
            Delete.Column("TemplateVariablesJson").FromTable("Monitor");
        }
    }
}
