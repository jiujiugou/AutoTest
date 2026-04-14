using System.Data;
using Auth.RBAC;
using Dapper;

namespace Auth;

/// <summary>
/// 基于 Dapper 的认证数据存储实现
/// </summary>
public sealed class DapperAuthStore : IAuthStore
{
    private readonly IDbConnection _db;

    /// <summary>
    /// 构造
    /// </summary>
    public DapperAuthStore(IDbConnection db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<int> CountUsersAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(1) FROM Users";
        var count = await _db.ExecuteScalarAsync<long>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return (int)count;
    }

    /// <inheritdoc />
    public Task<AuthUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var sql = IsSqlServer()
            ? """
              SELECT TOP 1
                u.Id,
                u.Username,
                u.PasswordHash,
                COALESCE(r.Name, '') AS Role,
                u.IsActive
              FROM Users u
              LEFT JOIN UserRoles ur ON ur.UserId = u.Id
              LEFT JOIN Roles r ON r.Id = ur.RoleId
              WHERE u.Username = @Username
              """
            : """
              SELECT
                u.Id,
                u.Username,
                u.PasswordHash,
                COALESCE(r.Name, '') AS Role,
                u.IsActive
              FROM Users u
              LEFT JOIN UserRoles ur ON ur.UserId = u.Id
              LEFT JOIN Roles r ON r.Id = ur.RoleId
              WHERE u.Username = @Username
              LIMIT 1
              """;

        var result= _db.QueryFirstOrDefaultAsync<AuthUser>(new CommandDefinition(
            sql,
            new { Username = username },
            cancellationToken: cancellationToken
        ));
        return result;
    }

    /// <inheritdoc />
    public Task<AuthUser?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var sql = IsSqlServer()
            ? """
              SELECT TOP 1
                u.Id,
                u.Username,
                u.PasswordHash,
                COALESCE(r.Name, '') AS Role,
                u.IsActive
              FROM Users u
              LEFT JOIN UserRoles ur ON ur.UserId = u.Id
              LEFT JOIN Roles r ON r.Id = ur.RoleId
              WHERE u.Id = @Id
              """
            : """
              SELECT
                u.Id,
                u.Username,
                u.PasswordHash,
                COALESCE(r.Name, '') AS Role,
                u.IsActive
              FROM Users u
              LEFT JOIN UserRoles ur ON ur.UserId = u.Id
              LEFT JOIN Roles r ON r.Id = ur.RoleId
              WHERE u.Id = @Id
              LIMIT 1
              """;

        return _db.QueryFirstOrDefaultAsync<AuthUser>(new CommandDefinition(
            sql,
            new { Id = id },
            cancellationToken: cancellationToken
        ));
    }

    /// <inheritdoc />
    public async Task<int> CreateUserAsync(string username, string passwordHash, string role, DateTime createdAt, CancellationToken cancellationToken)
    {
        int id;
        if (IsSqlServer())
        {
            const string sql = """
                               INSERT INTO Users (Username, PasswordHash, IsActive, CreatedAt, LastLoginAt)
                               OUTPUT INSERTED.Id
                               VALUES (@Username, @PasswordHash, 1, @CreatedAt, NULL);
                               """;

            id = await _db.ExecuteScalarAsync<int>(new CommandDefinition(
                sql,
                new { Username = username, PasswordHash = passwordHash, CreatedAt = createdAt },
                cancellationToken: cancellationToken
            ));
        }
        else
        {
            const string sql = """
                               INSERT INTO Users (Username, PasswordHash, IsActive, CreatedAt, LastLoginAt)
                               VALUES (@Username, @PasswordHash, 1, @CreatedAt, NULL);
                               SELECT last_insert_rowid();
                               """;

            var newId = await _db.ExecuteScalarAsync<long>(new CommandDefinition(
                sql,
                new { Username = username, PasswordHash = passwordHash, CreatedAt = createdAt },
                cancellationToken: cancellationToken
            ));

            id = (int)newId;
        }

        var ensureRoleSql = IsSqlServer()
            ? """
              IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = @Name)
              BEGIN
                  INSERT INTO Roles (Name, DisplayName, Description)
                  VALUES (@Name, @DisplayName, NULL);
              END
              """
            : """
              INSERT OR IGNORE INTO Roles (Name, DisplayName, Description)
              VALUES (@Name, @DisplayName, NULL);
              """;

        await _db.ExecuteAsync(new CommandDefinition(
            ensureRoleSql,
            new { Name = role, DisplayName = role },
            cancellationToken: cancellationToken
        ));

        const string userRoleSql = """
                                  INSERT INTO UserRoles (UserId, RoleId)
                                  SELECT @UserId, r.Id
                                  FROM Roles r
                                  WHERE r.Name = @RoleName
                                  """;

        await _db.ExecuteAsync(new CommandDefinition(
            userRoleSql,
            new { UserId = id, RoleName = role },
            cancellationToken: cancellationToken
        ));

        return id;
    }

    /// <inheritdoc />
    public Task<RefreshTokenRow?> GetRefreshTokenAsync(string token, CancellationToken cancellationToken)
    {
        var sql = IsSqlServer()
            ? """
              SELECT TOP 1 Id, UserId, Token, ExpireAt, Revoked, CreatedAt, ReplacedByToken
              FROM RefreshTokens
              WHERE Token = @Token
              """
            : """
              SELECT Id, UserId, Token, ExpireAt, Revoked, CreatedAt, ReplacedByToken
              FROM RefreshTokens
              WHERE Token = @Token
              LIMIT 1
              """;

        return _db.QueryFirstOrDefaultAsync<RefreshTokenRow>(new CommandDefinition(
            sql,
            new { Token = token },
            cancellationToken: cancellationToken
        ));
    }

    private bool IsSqlServer()
    {
        return _db.GetType().FullName?.Contains("SqlClient", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <inheritdoc />
    public Task AddRefreshTokenAsync(int userId, string token, DateTime expireAt, DateTime createdAt, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO RefreshTokens (UserId, Token, ExpireAt, Revoked, CreatedAt, ReplacedByToken)
                           VALUES (@UserId, @Token, @ExpireAt, 0, @CreatedAt, NULL)
                           """;

        return _db.ExecuteAsync(new CommandDefinition(
            sql,
            new { UserId = userId, Token = token, ExpireAt = expireAt, CreatedAt = createdAt },
            cancellationToken: cancellationToken
        ));
    }

    /// <inheritdoc />
    public Task RevokeRefreshTokenAsync(string token, string? replacedByToken, CancellationToken cancellationToken)
    {
        const string sql = """
                           UPDATE RefreshTokens
                           SET Revoked = 1, ReplacedByToken = @ReplacedByToken
                           WHERE Token = @Token
                           """;

        return _db.ExecuteAsync(new CommandDefinition(
            sql,
            new { Token = token, ReplacedByToken = replacedByToken },
            cancellationToken: cancellationToken
        ));
    }
}
