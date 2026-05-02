using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026050201)]
public sealed class AddRbacSoftDeleteColumns : Migration
{
    public override void Up()
    {
        if (!Schema.Table("Roles").Column("IsDeleted").Exists())
        {
            Alter.Table("Roles")
                .AddColumn("IsDeleted")
                .AsBoolean()
                .NotNullable()
                .WithDefaultValue(false);

            Create.Index("IX_Roles_IsDeleted")
                .OnTable("Roles")
                .OnColumn("IsDeleted");
        }

        if (!Schema.Table("Permissions").Column("IsDeleted").Exists())
        {
            Alter.Table("Permissions")
                .AddColumn("IsDeleted")
                .AsBoolean()
                .NotNullable()
                .WithDefaultValue(false);

            Create.Index("IX_Permissions_IsDeleted")
                .OnTable("Permissions")
                .OnColumn("IsDeleted");
        }
    }

    public override void Down()
    {
        if (Schema.Table("Roles").Column("IsDeleted").Exists())
        {
            Delete.Index("IX_Roles_IsDeleted").OnTable("Roles");
            Delete.Column("IsDeleted").FromTable("Roles");
        }

        if (Schema.Table("Permissions").Column("IsDeleted").Exists())
        {
            Delete.Index("IX_Permissions_IsDeleted").OnTable("Permissions");
            Delete.Column("IsDeleted").FromTable("Permissions");
        }
    }
}
