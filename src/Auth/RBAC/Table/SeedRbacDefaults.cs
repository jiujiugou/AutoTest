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

        // ================= 权限（分层命名空间） =================
        var perms = new[]
        {
            new { Code = "api.dashboard.view", Name = "查看仪表盘" },
            new { Code = "api.logs.view", Name = "查看日志" },

            new { Code = "api.monitor.view", Name = "查看监控" },
            new { Code = "api.monitor.run", Name = "运行监控" },
            new { Code = "api.monitor.create", Name = "创建监控" },
            new { Code = "api.monitor.update", Name = "更新监控" },
            new { Code = "api.monitor.delete", Name = "删除监控" },

            new { Code = "api.tasks.view", Name = "查看任务" },
            new { Code = "api.tasks.manage", Name = "任务管理" },

            new { Code = "api.settings.view", Name = "查看设置" },
            new { Code = "api.settings.manage", Name = "管理设置" },

            new { Code = "ui.menu.dashboard", Name = "仪表盘菜单" },
            new { Code = "ui.menu.monitor", Name = "监控观测菜单" },
            new { Code = "ui.menu.task", Name = "任务调度菜单" },
            new { Code = "ui.menu.log", Name = "系统日志菜单" },
            new { Code = "ui.menu.settings", Name = "系统设置菜单" },
            new { Code = "ui.menu.ai", Name = "AI助手菜单" },
            new { Code = "ui.menu.rbac", Name = "权限管理菜单" },
            new { Code = "ui.menu.template", Name = "模板设计菜单" },
            new { Code = "ui.menu.person", Name = "个人中心菜单" }
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
                                            JOIN Permissions p ON p.Code IN ('api.dashboard.view','api.logs.view','api.monitor.view','api.tasks.view','ui.menu.person')
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
