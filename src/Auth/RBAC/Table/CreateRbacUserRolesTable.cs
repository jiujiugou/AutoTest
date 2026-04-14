using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026041103)]
public sealed class CreateRbacUserRolesTable : Migration
{
    public override void Up()
    {
        Create.Table("UserRoles")
            .WithColumn("UserId").AsInt32().NotNullable().ForeignKey("Users", "Id")
            .WithColumn("RoleId").AsInt32().NotNullable().ForeignKey("Roles", "Id");

        Create.Index("UX_UserRoles_UserId_RoleId")
            .OnTable("UserRoles")
            .OnColumn("UserId").Ascending()
            .OnColumn("RoleId").Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        Delete.Table("UserRoles");
    }
}
