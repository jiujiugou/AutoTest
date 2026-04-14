using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026041101)]
public sealed class CreateRbacRolesTable : Migration
{
    public override void Up()
    {
        Create.Table("Roles")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Name").AsString(50).NotNullable()
            .WithColumn("DisplayName").AsString(100).NotNullable()
            .WithColumn("Description").AsString(255).Nullable();

        Create.Index("UX_Roles_Name")
            .OnTable("Roles")
            .OnColumn("Name").Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        Delete.Table("Roles");
    }
}
