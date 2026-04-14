using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026041001)]
public sealed class CreateUsersTable : Migration
{
    public override void Up()
    {
        Create.Table("Users")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Username").AsString(50).NotNullable().Unique()
            .WithColumn("PasswordHash").AsString(256).NotNullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("LastLoginAt").AsDateTime().Nullable();
    }

    public override void Down()
    {
        Delete.Table("Users");
    }
}

