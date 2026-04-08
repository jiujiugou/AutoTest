using FluentMigrator;
using System.Data;

namespace AutoTest.Migrations;

[Migration(2026032203)]
public class InitAssertionResultTable : Migration
{
    public override void Down()
    {
        Delete.Table("AssertionResult");
    }

    public override void Up()
    {
        Create.Table("AssertionResult")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("AssertionId").AsGuid().NotNullable()
            .ForeignKey("FK_AssertionResult_Assertion", "Assertion", "Id")
            .OnDeleteOrUpdate(Rule.Cascade)
            .WithColumn("Target").AsString(100).NotNullable()
            .WithColumn("IsSuccess").AsBoolean().NotNullable()
            .WithColumn("Actual").AsString(int.MaxValue).Nullable()
            .WithColumn("Expected").AsString(int.MaxValue).Nullable()
            .WithColumn("Message").AsString(int.MaxValue).Nullable()
            .WithColumn("Timestamp").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);
    }
}
