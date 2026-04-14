using System.Data;
using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Auth;

/// <summary>
/// 基于 Dapper + 内存缓存的权限存储实现
/// </summary>
public sealed class DapperPermissionStore : IPermissionStore
{
    private readonly IDbConnection _db;
    private readonly IMemoryCache _cache;
    private readonly PermissionOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="db">数据库连接</param>
    /// <param name="cache">内存缓存</param>
    /// <param name="options">权限配置项</param>
    public DapperPermissionStore(IDbConnection db, IMemoryCache cache, IOptions<PermissionOptions> options)
    {
        _db = db;
        _cache = cache;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken)
    {
        var cacheKey = $"perm:{userId}:{permission}";
        if (_cache.TryGetValue(cacheKey, out bool hit))
            return hit;

        const string sql = """
                           SELECT EXISTS(
                               SELECT 1
                               FROM Users u
                               JOIN UserRoles ur ON ur.UserId = u.Id
                               JOIN RolePermissions rp ON rp.RoleId = ur.RoleId
                               JOIN Permissions p ON p.Id = rp.PermissionId
                               WHERE u.Username = @UserId
                                 AND u.IsActive = 1
                                 AND p.Code = @Permission
                           )
                           """;

        var exists = await _db.ExecuteScalarAsync<long>(new CommandDefinition(
            sql,
            new { UserId = userId, Permission = permission },
            cancellationToken: cancellationToken
        ));

        var allowed = exists == 1;
        _cache.Set(cacheKey, allowed, _options.CacheTtl);
        return allowed;
    }
}
