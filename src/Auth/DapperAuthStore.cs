using System.Data;
using Auth.RBAC;
using AutoTest.Core.Auth;
using Dapper;

namespace Auth;

/// <summary>
/// 基于 Dapper 的认证数据存储实现
/// 
/// 职责说明：
/// - 用户 CRUD
/// - RBAC 角色绑定
/// - RefreshToken 管理
/// - 权限查询（RBAC）
/// 
/// ⚠ 注意：
/// 当前实现为“轻量 RBAC 实现”
/// </summary>
public sealed class DapperAuthStore : IAuthStore
{
    private readonly IDbConnection _db;

    public DapperAuthStore(IDbConnection db)
    {
        _db = db;
    }

    // =========================
    // Users（用户基础查询）
    // =========================

    /// <summary>
    /// 统计未删除用户数量（用于后台统计/健康检查）
    /// </summary>
    public async Task<int> CountUsersAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(1) FROM Users WHERE IsDeleted = 0";

        var count = await _db.ExecuteScalarAsync<long>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return (int)count;
    }

    /// <summary>
    /// 根据用户名获取用户（包含角色聚合）
    /// 
    /// ⚠ 关键点：
    /// - 使用 STRING_AGG 解决“一用户多角色导致多行问题”
    /// - 过滤软删除用户
    /// </summary>
    public Task<AuthUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        const string sql = """
    SELECT
        u.Id,
        u.Username,
        u.PasswordHash,
        COALESCE(STRING_AGG(r.Name, ',') WITHIN GROUP (ORDER BY r.Name), NULL) AS Role,
        u.IsActive
    FROM Users u
    LEFT JOIN UserRoles ur ON ur.UserId = u.Id
    LEFT JOIN Roles r ON r.Id = ur.RoleId
    WHERE u.Username = @Username
      AND u.IsDeleted = 0
    GROUP BY u.Id, u.Username, u.PasswordHash, u.IsActive
    """;

        return _db.QueryFirstOrDefaultAsync<AuthUser>(
            new CommandDefinition(sql, new { Username = username }, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// 根据用户ID获取用户（同样包含角色聚合）
    /// </summary>
    public Task<AuthUser?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = """
        SELECT TOP 1
            u.Id,
            u.Username,
            u.PasswordHash,
            COALESCE(STRING_AGG(r.Name, ','), '') AS Role,
            u.IsActive
        FROM Users u
        LEFT JOIN UserRoles ur ON ur.UserId = u.Id
        LEFT JOIN Roles r ON r.Id = ur.RoleId 
        WHERE u.Id = @Id
          AND u.IsDeleted = 0
        GROUP BY u.Id, u.Username, u.PasswordHash, u.IsActive
        """;

        return _db.QueryFirstOrDefaultAsync<AuthUser>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    // =========================
    // Create User（用户创建 + RBAC绑定）
    // =========================

    /// <summary>
    /// 创建用户 + 初始化角色绑定
    /// 
    /// 流程：
    /// 1. 创建用户
    /// 2. 确保角色存在（MERGE防并发）
    /// 3. 绑定用户角色
    /// 
    /// ⚠ 关键风险已修复：
    /// - Role 并发插入问题（MERGE）
    /// - Role不存在 silent fail（THROW）
    /// </summary>
    public async Task<int> CreateUserAsync(
        string username,
        string passwordHash,
        string role,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        // 1. 插入用户（返回ID）
        const string insertUserSql = """
        INSERT INTO Users (Username, PasswordHash, IsActive, CreatedAt, LastLoginAt)
        OUTPUT INSERTED.Id
        VALUES (@Username, @PasswordHash, 1, @CreatedAt, NULL);
        """;

        var userId = await _db.ExecuteScalarAsync<int>(
            new CommandDefinition(
                insertUserSql,
                new { Username = username, PasswordHash = passwordHash, CreatedAt = createdAt },
                cancellationToken: cancellationToken));

        // 2. 确保角色存在（MERGE防止并发重复插入）
        const string roleSql = """
        MERGE Roles AS t
        USING (SELECT @Name AS Name, @DisplayName AS DisplayName) s
        ON t.Name = s.Name
        WHEN NOT MATCHED THEN
            INSERT (Name, DisplayName, Description)
            VALUES (s.Name, s.DisplayName, NULL);
        """;

        await _db.ExecuteAsync(
            new CommandDefinition(roleSql,
                new { Name = role, DisplayName = role },
                cancellationToken: cancellationToken));

        // 3. 绑定用户角色（不存在则抛异常）
        const string userRoleSql = """
        INSERT INTO UserRoles (UserId, RoleId)
        SELECT @UserId, r.Id
        FROM Roles r
        WHERE r.Name = @RoleName;

        IF @@ROWCOUNT = 0
            THROW 50001, 'Role not found', 1;
        """;

        await _db.ExecuteAsync(
            new CommandDefinition(userRoleSql,
                new { UserId = userId, RoleName = role },
                cancellationToken: cancellationToken));

        return userId;
    }

    // =========================
    // Refresh Token（JWT刷新机制）
    // =========================

    /// <summary>
    /// 获取有效 RefreshToken
    /// 
    /// ⚠ 安全策略：
    /// - 未撤销
    /// - 未过期
    /// </summary>
    public Task<RefreshTokenRow?> GetRefreshTokenAsync(string token, CancellationToken cancellationToken)
    {
        const string sql = """
        SELECT TOP 1 Id, UserId, Token, ExpireAt, Revoked, CreatedAt, ReplacedByToken
        FROM RefreshTokens
        WHERE Token = @Token
          AND Revoked = 0
          AND ExpireAt > GETUTCDATE()
        """;

        return _db.QueryFirstOrDefaultAsync<RefreshTokenRow>(
            new CommandDefinition(sql, new { Token = token }, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// 添加 RefreshToken
    /// </summary>
    public Task AddRefreshTokenAsync(
        int userId,
        string token,
        DateTime expireAt,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
        INSERT INTO RefreshTokens (UserId, Token, ExpireAt, Revoked, CreatedAt, ReplacedByToken)
        VALUES (@UserId, @Token, @ExpireAt, 0, @CreatedAt, NULL)
        """;

        return _db.ExecuteAsync(
            new CommandDefinition(sql,
                new { UserId = userId, Token = token, ExpireAt = expireAt, CreatedAt = createdAt },
                cancellationToken: cancellationToken));
    }

    /// <summary>
    /// 撤销 RefreshToken（用于换token / 登出）
    /// </summary>
    public Task RevokeRefreshTokenAsync(string token, string? replacedByToken, CancellationToken cancellationToken)
    {
        const string sql = """
        UPDATE RefreshTokens
        SET Revoked = 1,
            ReplacedByToken = @ReplacedByToken
        WHERE Token = @Token
        """;

        return _db.ExecuteAsync(
            new CommandDefinition(sql,
                new { Token = token, ReplacedByToken = replacedByToken },
                cancellationToken: cancellationToken));
    }

    // =========================
    // Profile（用户资料）
    // =========================

    /// <summary>
    /// 更新密码（仅更新Hash）
    /// </summary>
    public async Task<bool> UpdatePasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken)
    {
        const string sql = """
        UPDATE Users
        SET PasswordHash = @PasswordHash
        WHERE Id = @UserId
        """;

        var rows = await _db.ExecuteAsync(
            new CommandDefinition(sql,
                new { UserId = userId, PasswordHash = passwordHash },
                cancellationToken: cancellationToken));

        return rows > 0;
    }

    /// <summary>
    /// 更新用户基础信息
    /// </summary>
    public async Task<bool> UpdateUserProfileAsync(
        int userId,
        string username,
        bool isActive,
        CancellationToken cancellationToken)
    {
        const string sql = """
        UPDATE Users
        SET Username = @Username,
            IsActive = @IsActive
        WHERE Id = @UserId
        """;

        var rows = await _db.ExecuteAsync(
            new CommandDefinition(sql,
                new { UserId = userId, Username = username, IsActive = isActive },
                cancellationToken: cancellationToken));

        return rows > 0;
    }

    public async Task UpdateLastLoginAsync(int userId, DateTime loginTime, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Users SET LastLoginAt = @LoginTime WHERE Id = @UserId";
        await _db.ExecuteAsync(new CommandDefinition(sql, new { UserId = userId, LoginTime = loginTime }, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// 软删除用户（保留数据用于审计）
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        const string sql = """
        UPDATE Users
        SET IsDeleted = 1,
            IsActive = 0
        WHERE Id = @Id
        """;

        var rows = await _db.ExecuteAsync(new CommandDefinition(sql, new { Id = id }));
        return rows > 0;
    }

    // =========================
    // Roles（用户角色绑定）
    // =========================

    /// <summary>
    /// 重置用户角色（单角色模型）
    /// 
    /// ⚠ 注意：
    /// 当前设计是“一个用户一个角色”（覆盖式）
    /// </summary>
    public async Task<bool> SetUserRoleAsync(int userId, string roleName, CancellationToken cancellationToken)
    {
        // 删除旧角色
        const string deleteSql = """
        DELETE FROM UserRoles WHERE UserId = @UserId
        """;

        await _db.ExecuteAsync(
            new CommandDefinition(deleteSql,
                new { UserId = userId },
                cancellationToken: cancellationToken));

        // 插入新角色
        const string insertSql = """
        INSERT INTO UserRoles (UserId, RoleId)
        SELECT @UserId, r.Id
        FROM Roles r
        WHERE r.Name = @RoleName;

        IF @@ROWCOUNT = 0
            THROW 50001, 'Role not found', 1;
        """;

        var rows = await _db.ExecuteAsync(
            new CommandDefinition(insertSql,
                new { UserId = userId, RoleName = roleName },
                cancellationToken: cancellationToken));

        return rows > 0;
    }

    // =========================
    // Permissions（RBAC权限查询）
    // =========================

    /// <summary>
    /// 获取用户权限（RBAC链路）
    /// 
    /// 权限路径：
    /// User → Role → RolePermissions → Permissions
    /// 
    /// 如果是 admin 角色，直接返回全部权限，不查 RolePermissions。
    /// </summary>
    public async Task<IReadOnlyList<string>> GetUserPermissionsAsync(int userId, CancellationToken cancellationToken)
    {
        // 1. 先判断用户角色
        var roleSql = """
        SELECT TOP 1 r.Name
        FROM UserRoles ur
        JOIN Roles r ON r.Id = ur.RoleId AND r.IsDeleted = 0
        WHERE ur.UserId = @UserId
        """;

        var roleName = await _db.QueryFirstOrDefaultAsync<string>(
            new CommandDefinition(roleSql, new { UserId = userId }, cancellationToken: cancellationToken));

        // 2. admin 角色直接返回全部权限
        if (string.Equals(roleName, "admin", StringComparison.OrdinalIgnoreCase))
        {
            const string allPermsSql = """
            SELECT Code FROM Permissions WHERE IsDeleted = 0
            """;
            var allPerms = await _db.QueryAsync<string>(
                new CommandDefinition(allPermsSql, cancellationToken: cancellationToken));
            return allPerms.ToList();
        }

        // 3. 非 admin 走正常 RBAC 链路
        const string sql = """
        SELECT DISTINCT p.Code
        FROM Users u
        JOIN UserRoles ur ON ur.UserId = u.Id
        JOIN Roles r ON r.Id = ur.RoleId AND r.IsDeleted = 0
        JOIN RolePermissions rp ON rp.RoleId = r.Id
        JOIN Permissions p ON p.Id = rp.PermissionId AND p.IsDeleted = 0
        WHERE u.Id = @UserId
          AND u.IsDeleted = 0
        """;

        var result = await _db.QueryAsync<string>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));

        return result.ToList();
    }
}