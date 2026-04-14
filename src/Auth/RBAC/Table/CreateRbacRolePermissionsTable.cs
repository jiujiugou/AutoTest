using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026041104)]
public sealed class CreateRbacRolePermissionsTable : Migration
{
    public override void Up()
    {
        Create.Table("RolePermissions")
            .WithColumn("RoleId").AsInt32().NotNullable().ForeignKey("Roles", "Id")
            .WithColumn("PermissionId").AsInt32().NotNullable().ForeignKey("Permissions", "Id");

        Create.Index("UX_RolePermissions_RoleId_PermissionId")
            .OnTable("RolePermissions")
            .OnColumn("RoleId").Ascending()
            .OnColumn("PermissionId").Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        Delete.Table("RolePermissions");
    }
}
