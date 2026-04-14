using Auth.RBAC;
using System.Data;
using Dapper;
using Auth.RBAC;
using AutoTest.Application;
namespace Auth
{

    /// <summary>
    /// RBAC 权限服务实现（基于 Dapper + UnitOfWork）
    /// </summary>
    public class RbacService : IRbacService
    {
        private readonly IDbConnection _db;
        private readonly IUnitOfWork _uow;

        /// <summary>
        /// 构造函数
        /// </summary>
        public RbacService(IDbConnection db, IUnitOfWork uow)
        {
            _db = db;
            _uow = uow;
        }

        /// <summary>
        /// 获取所有角色
        /// </summary>
        public Task<IEnumerable<Role>> GetRolesAsync()
        {
            const string sql = """
            SELECT Id, Name, DisplayName, Description
            FROM Roles
            ORDER BY Id ASC
        """;

            return _db.QueryAsync<Role>(sql);
        }

        /// <summary>
        /// 获取所有权限
        /// </summary>
        public Task<IEnumerable<Permission>> GetPermissionsAsync()
        {
            const string sql = """
            SELECT Id, Code, Name, Description
            FROM Permissions
            ORDER BY Id ASC
        """;

            return _db.QueryAsync<Permission>(sql);
        }

        /// <summary>
        /// 获取指定角色的权限列表
        /// </summary>
        public Task<IEnumerable<RolePermission>> GetRolePermissionsAsync(int roleId)
        {
            const string sql = """
            SELECT p.Id, p.Code
            FROM RolePermissions rp
            JOIN Permissions p ON p.Id = rp.PermissionId
            WHERE rp.RoleId = @RoleId
            ORDER BY p.Id ASC
        """;

            return _db.QueryAsync<RolePermission>(sql, new { RoleId = roleId });
        }

        /// <summary>
        /// 获取用户列表（分页限制）
        /// </summary>
        public Task<IEnumerable<AuthUser>> GetUsersAsync(int take)
        {
            var sql = IsSqlServer(_db)
                ? """
                  SELECT Id, Username, IsActive, CreatedAt, LastLoginAt
                  FROM Users
                  ORDER BY Id ASC
                  OFFSET 0 ROWS FETCH NEXT @Take ROWS ONLY
                  """
                : """
                  SELECT Id, Username, IsActive, CreatedAt, LastLoginAt
                  FROM Users
                  ORDER BY Id ASC
                  LIMIT @Take
                  """;

            return _db.QueryAsync<AuthUser>(sql, new { Take = take });
        }

        /// <summary>
        /// 获取用户当前角色（单角色模式）
        /// </summary>
        public Task<UserRole?> GetUserRoleAsync(int userId)
        {
            var sql = IsSqlServer(_db)
                ? """
                  SELECT TOP 1 r.Id, r.Name, r.DisplayName
                  FROM UserRoles ur
                  JOIN Roles r ON r.Id = ur.RoleId
                  WHERE ur.UserId = @UserId
                  """
                : """
                  SELECT r.Id, r.Name, r.DisplayName
                  FROM UserRoles ur
                  JOIN Roles r ON r.Id = ur.RoleId
                  WHERE ur.UserId = @UserId
                  LIMIT 1
                  """;

            return _db.QueryFirstOrDefaultAsync<UserRole>(sql, new { UserId = userId });
        }

        /// <summary>
        /// 设置角色权限（覆盖旧权限）
        /// </summary>
        public Task SetRolePermissionsAsync(int roleId, string[] codes)
        {
            return _uow.ExecuteAsync(async tx =>
            {
                codes ??= Array.Empty<string>();

                // 1. 清理 & 规范化权限Code
                var normalized = codes
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                // 2. 删除旧权限
                await _db.ExecuteAsync(
                    "DELETE FROM RolePermissions WHERE RoleId = @RoleId",
                    new { RoleId = roleId },
                    tx);

                // 3. 插入新权限
                const string sql = """
                INSERT INTO RolePermissions (RoleId, PermissionId)
                SELECT @RoleId, Id
                FROM Permissions
                WHERE Code = @Code
            """;

                foreach (var code in normalized)
                {
                    await _db.ExecuteAsync(sql, new
                    {
                        RoleId = roleId,
                        Code = code
                    }, tx);
                }
            });
        }

        /// <summary>
        /// 设置用户角色（单角色绑定）
        /// </summary>
        public Task SetUserRoleAsync(int userId, string roleName)
        {
            return _uow.ExecuteAsync(async tx =>
            {
                if (string.IsNullOrWhiteSpace(roleName))
                    throw new ArgumentException("roleName 不能为空");

                // 1. 查找角色ID
                var roleIdSql = IsSqlServer(_db)
                    ? "SELECT TOP 1 Id FROM Roles WHERE Name = @Name"
                    : "SELECT Id FROM Roles WHERE Name = @Name LIMIT 1";
                var roleId = await _db.ExecuteScalarAsync<long?>(
                    roleIdSql,
                    new { Name = roleName.Trim() },
                    tx);

                if (!roleId.HasValue)
                    throw new Exception("角色不存在");

                // 2. 删除旧角色
                await _db.ExecuteAsync(
                    "DELETE FROM UserRoles WHERE UserId = @UserId",
                    new { UserId = userId },
                    tx);

                // 3. 插入新角色
                await _db.ExecuteAsync(
                    "INSERT INTO UserRoles (UserId, RoleId) VALUES (@UserId, @RoleId)",
                    new
                    {
                        UserId = userId,
                        RoleId = (int)roleId.Value
                    },
                    tx);
            });
        }

        private static bool IsSqlServer(IDbConnection db)
        {
            return db.GetType().FullName?.Contains("SqlClient", StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
