using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026041102)]
public sealed class CreateRbacPermissionsTable : Migration
{
    public override void Up()
    {
        Create.Table("Permissions")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Code").AsString(100).NotNullable()
            .WithColumn("Name").AsString(100).NotNullable()
            .WithColumn("Description").AsString(255).Nullable();

        Create.Index("UX_Permissions_Code")
            .OnTable("Permissions")
            .OnColumn("Code").Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        Delete.Table("Permissions");
    }
}
