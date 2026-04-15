using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026041105)]
public sealed class SeedRbacDefaults : Migration
{
    public override void Up()
    {
        IfDatabase("SqlServer").Execute.Sql("""
                                            IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'admin')
                                                INSERT INTO Roles (Name, DisplayName, Description)
                                                VALUES ('admin', N'管理员', N'平台管理员');
                                            """);
        IfDatabase("SqlServer").Execute.Sql("""
                                            IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'user')
                                                INSERT INTO Roles (Name, DisplayName, Description)
                                                VALUES ('user', N'用户', N'普通用户');
                                            """);

        // ================= 权限 =================
        var perms = new[]
        {
            new { Code = "dashboard.view", Name = "查看仪表盘" },
            new { Code = "logs.view", Name = "查看日志" },

            new { Code = "monitor.view", Name = "查看监控" },
            new { Code = "monitor.run", Name = "运行监控" },
            new { Code = "monitor.create", Name = "创建监控" },
            new { Code = "monitor.update", Name = "更新监控" },
            new { Code = "monitor.delete", Name = "删除监控" },

            new { Code = "tasks.view", Name = "查看任务" },
            new { Code = "tasks.manage", Name = "任务管理" },

            new { Code = "settings.view", Name = "查看设置" },
            new { Code = "settings.manage", Name = "管理设置" }
        };

        foreach (var p in perms)
        {
            var code = p.Code.Replace("'", "''");
            var name = p.Name.Replace("'", "''");

            IfDatabase("SqlServer").Execute.Sql($"""
                                                IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Code = '{code}')
                                                    INSERT INTO Permissions (Code, Name)
                                                    VALUES ('{code}', N'{name}');
                                                """);

            IfDatabase("SQLite").Execute.Sql($"""
                                             INSERT OR IGNORE INTO Permissions (Code, Name)
                                             VALUES ('{code}', '{name}');
                                             """);
        }

        // ================= admin：全部权限 =================
        IfDatabase("SqlServer").Execute.Sql("""
                                            INSERT INTO RolePermissions (RoleId, PermissionId)
                                            SELECT r.Id, p.Id
                                            FROM Roles r
                                            CROSS JOIN Permissions p
                                            WHERE r.Name = 'admin'
                                              AND NOT EXISTS (
                                                SELECT 1 FROM RolePermissions rp
                                                WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id
                                              );
                                            """);

        // ================= user：基础权限 =================
        IfDatabase("SqlServer").Execute.Sql("""
                                            INSERT INTO RolePermissions (RoleId, PermissionId)
                                            SELECT r.Id, p.Id
                                            FROM Roles r
                                            JOIN Permissions p ON p.Code IN ('dashboard.view','logs.view','monitor.view','tasks.view')
                                            WHERE r.Name = 'user'
                                              AND NOT EXISTS (
                                                SELECT 1 FROM RolePermissions rp
                                                WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id
                                              );
                                            """);
    }

    public override void Down()
    {
        Delete.FromTable("RolePermissions").AllRows();
        Delete.FromTable("UserRoles").AllRows();
        Delete.FromTable("Permissions").AllRows();
        Delete.FromTable("Roles").AllRows();
    }
}
