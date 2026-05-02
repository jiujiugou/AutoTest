using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026041701)]
public sealed class AddUsersSoftDeleteColumn : Migration
{
    public override void Up()
    {
        // 增加软删除字段
        if (!Schema.Table("Users").Column("IsDeleted").Exists())
        {
            Alter.Table("Users")
                .AddColumn("IsDeleted")
                .AsBoolean()
                .NotNullable()
                .WithDefaultValue(false);
        }

        // 加索引
        if (!Schema.Table("Users").Index("IX_Users_IsDeleted").Exists())
        {
            Create.Index("IX_Users_IsDeleted")
                .OnTable("Users")
                .OnColumn("IsDeleted");
        }
    }

    public override void Down()
    {
        Delete.Index("IX_Users_IsDeleted").OnTable("Users");

        Delete.Column("IsDeleted").FromTable("Users");
    }
}