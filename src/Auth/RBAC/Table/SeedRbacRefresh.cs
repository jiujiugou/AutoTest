using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026050202)]
public sealed class SeedRbacRefresh : Migration
{
    private static readonly (string Code, string Name)[] AllPermissions =
    [
        ("api.dashboard.view", "查看仪表盘"),
        ("api.logs.view", "查看日志"),

        ("api.monitor.view", "查看监控"),
        ("api.monitor.run", "运行监控"),
        ("api.monitor.create", "创建监控"),
        ("api.monitor.update", "更新监控"),
        ("api.monitor.delete", "删除监控"),

        ("api.tasks.view", "查看任务"),
        ("api.tasks.manage", "任务管理"),

        ("api.settings.view", "查看设置"),
        ("api.settings.manage", "管理设置"),

        ("ui.menu.dashboard", "仪表盘菜单"),
        ("ui.menu.monitor", "监控观测菜单"),
        ("ui.menu.task", "任务调度菜单"),
        ("ui.menu.log", "系统日志菜单"),
        ("ui.menu.settings", "系统设置菜单"),
        ("ui.menu.ai", "AI助手菜单"),
        ("ui.menu.rbac", "权限管理菜单"),
        ("ui.menu.template", "模板设计菜单"),
        ("ui.menu.person", "个人中心菜单")
    ];

    private static readonly string[] AllPermissionCodes = AllPermissions.Select(p => p.Code).ToArray();

    private static readonly string[] UserPermissionCodes =
    [
        "api.dashboard.view",
        "api.logs.view",
        "api.monitor.view",
        "api.tasks.view",
        "ui.menu.person"
    ];

    public override void Up()
    {
        IfDatabase("SqlServer").Execute.Sql("""
            IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'admin')
                INSERT INTO Roles (Name, DisplayName, Description)
                VALUES ('admin', N'管理员', N'平台管理员');

            IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'user')
                INSERT INTO Roles (Name, DisplayName, Description)
                VALUES ('user', N'用户', N'普通用户');
            """);

        var codes = string.Join(",", AllPermissionCodes.Select(c => $"'{c.Replace("'", "''")}'"));
        IfDatabase("SqlServer").Execute.Sql($"DELETE FROM Permissions WHERE Code NOT IN ({codes})");

        foreach (var (code, name) in AllPermissions)
        {
            var safeCode = code.Replace("'", "''");
            var safeName = name.Replace("'", "''");

            IfDatabase("SqlServer").Execute.Sql($"""
                IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Code = '{safeCode}')
                    INSERT INTO Permissions (Code, Name) VALUES ('{safeCode}', N'{safeName}');
                """);
        }

        IfDatabase("SqlServer").Execute.Sql("""
            DELETE rp FROM RolePermissions rp JOIN Roles r ON r.Id = rp.RoleId WHERE r.Name = 'admin';
            INSERT INTO RolePermissions (RoleId, PermissionId)
            SELECT r.Id, p.Id FROM Roles r CROSS JOIN Permissions p WHERE r.Name = 'admin';
            """);

        var userCodes = string.Join(",", UserPermissionCodes.Select(c => $"'{c.Replace("'", "''")}'"));
        IfDatabase("SqlServer").Execute.Sql($"""
            DELETE rp FROM RolePermissions rp JOIN Roles r ON r.Id = rp.RoleId WHERE r.Name = 'user';
            INSERT INTO RolePermissions (RoleId, PermissionId)
            SELECT r.Id, p.Id FROM Roles r JOIN Permissions p ON p.Code IN ({userCodes}) WHERE r.Name = 'user';
            """);
    }

    public override void Down()
    {
    }
}
