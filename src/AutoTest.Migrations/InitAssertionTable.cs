using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026032202)]
public class InitAssertionTable : Migration
{
    public override void Down()
    {
        Delete.Table("Assertion");
    }

    public override void Up()
    {
        Create.Table("Assertion")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("MonitorId").AsGuid().NotNullable().ForeignKey("Monitor", "Id")
            .WithColumn("Type").AsString(50).NotNullable()          // Http / Tcp / Db
            .WithColumn("ConfigJson").AsString(int.MaxValue).NotNullable(); // 断言 JSON 配置
    }

}
