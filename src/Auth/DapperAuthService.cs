using System.Collections.Concurrent;
using System.Data;
using System.Security.Cryptography;
using Auth.RBAC;
using Dapper;
using Microsoft.AspNetCore.WebUtilities;

namespace Auth;

/// <summary>
/// 基于 Dapper 存储的认证服务实现：登录/刷新/登出/初始化管理员
/// </summary>
public sealed class DapperAuthService : IAuthService
{
    private readonly IAuthStore _store;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenIssuer _tokenIssuer;
    private readonly IDbConnection _db;

    private static readonly ConcurrentDictionary<string, (int Attempts, DateTime LockedUntil)> _failedLogins = new();
    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 15;

    /// <summary>
    /// 构造
    /// </summary>
    public DapperAuthService(IAuthStore store, IPasswordHasher passwordHasher, ITokenIssuer tokenIssuer, IDbConnection db)
    {
        _store = store;
        _passwordHasher = passwordHasher;
        _tokenIssuer = tokenIssuer;
        _db = db;
    }

    /// <summary>
    /// 获取用户权限列表。admin 角色直接返回所有权限，不走 RolePermissions 表。
    /// </summary>
    private async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(AuthUser user, CancellationToken cancellationToken)
    {
        if (string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            var all = await _db.QueryAsync<string>(
                new CommandDefinition("SELECT Code FROM Permissions WHERE IsDeleted = 0", cancellationToken: cancellationToken));
            return all.ToList();
        }

        return await _store.GetUserPermissionsAsync(user.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task BootstrapAdminAsync(string username, string password, CancellationToken cancellationToken)
    {
        var count = await _store.CountUsersAsync(cancellationToken);
        if (count != 0)
            throw new InvalidOperationException("Users already exist");

        var hash = _passwordHasher.Hash(password);
        await _store.CreateUserAsync(username, hash, "admin", DateTime.UtcNow, cancellationToken);
    }
    public async Task AddUserAsync(string username, string password, CancellationToken cancellationToken)
    {
        var count=await _store.CountUsersAsync(cancellationToken);
        if (count != 0)
        {
            throw new InvalidOperationException("User already exist");
        }
        var hash = _passwordHasher.Hash(password);
        await _store.CreateUserAsync(username, hash, "user", DateTime.UtcNow, cancellationToken);
    }

    public async Task<int> CreateUserAsync(string username, string password, string role, CancellationToken cancellationToken)
    {
        var hash = _passwordHasher.Hash(password);
        return await _store.CreateUserAsync(username, hash, role, DateTime.UtcNow, cancellationToken);
    }

    public async Task UpdateUserProfileAsync(int userId, string username, bool isActive, CancellationToken cancellationToken)
    {
        await _store.UpdateUserProfileAsync(userId, username, isActive, cancellationToken);
    }

    public async Task DeleteUserAsync(int userId, CancellationToken cancellationToken)
    {
        await _store.DeleteAsync(userId);
    }
    /// <inheritdoc />
    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken)
    {
        // Check account lockout
        var key = username.ToLowerInvariant();
        if (_failedLogins.TryGetValue(key, out var entry))
        {
            if (entry.LockedUntil > DateTime.UtcNow)
                return null; // Still locked — don't leak whether the account exists
            if (entry.LockedUntil <= DateTime.UtcNow)
                _failedLogins.TryRemove(key, out _);
        }

        var user = await _store.GetByUsernameAsync(username, cancellationToken);
        if (user == null || !user.IsActive)
            return null;

        if (!_passwordHasher.Verify(password, user.PasswordHash))
        {
            TrackFailedLogin(key);
            return null;
        }

        // Successful login — clear failed attempts
        _failedLogins.TryRemove(key, out _);

        await _store.UpdateLastLoginAsync(user.Id, DateTime.UtcNow, cancellationToken);

        var permissions = await GetEffectivePermissionsAsync(user, cancellationToken);
        var accessToken = _tokenIssuer.GenerateAccessToken(user.Username, user.Role, permissions);
        var refreshToken = GenerateRefreshToken();
        await _store.AddRefreshTokenAsync(user.Id, refreshToken, DateTime.UtcNow.AddDays(30), DateTime.UtcNow, cancellationToken);
        return new LoginResult(accessToken, refreshToken)
        {
            User = new LoginUserInfo
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role,
                Permissions = permissions.ToList()
            }
        };
    }

    private static void TrackFailedLogin(string key)
    {
        _failedLogins.AddOrUpdate(key,
            _ => (1, DateTime.UtcNow.AddMinutes(LockoutMinutes)),
            (_, existing) =>
            {
                var newAttempts = existing.Attempts + 1;
                if (newAttempts >= MaxFailedAttempts)
                    return (newAttempts, DateTime.UtcNow.AddMinutes(LockoutMinutes));
                return (newAttempts, existing.LockedUntil);
            });
    }

    /// <inheritdoc />
    public async Task<LoginResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var row = await _store.GetRefreshTokenAsync(refreshToken, cancellationToken);
        if (row == null || row.Revoked || row.ExpireAt <= DateTime.UtcNow)
            return null;

        var user = await _store.GetByIdAsync(row.UserId, cancellationToken);
        if (user == null || !user.IsActive)
            return null;

        var permissions = await GetEffectivePermissionsAsync(user, cancellationToken);
        var accessToken = _tokenIssuer.GenerateAccessToken(user.Username, user.Role, permissions);
        var newRefreshToken = GenerateRefreshToken();
        await _store.RevokeRefreshTokenAsync(refreshToken, newRefreshToken, cancellationToken);
        await _store.AddRefreshTokenAsync(user.Id, newRefreshToken, DateTime.UtcNow.AddDays(30), DateTime.UtcNow, cancellationToken);
        return new LoginResult(accessToken, newRefreshToken)
        {
            User = new LoginUserInfo
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role,
                Permissions = permissions.ToList()
            }
        };
    }

    /// <inheritdoc />
    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        await _store.RevokeRefreshTokenAsync(refreshToken, null, cancellationToken);
    }

    /// <summary>
    /// 生成随机 RefreshToken（URL 安全 Base64）
    /// </summary>
    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    public async Task UpdateUserPasswordAsync(int id, string password, CancellationToken cancellationToken)
    {
        var hash = _passwordHasher.Hash(password);
        await _store.UpdatePasswordAsync(id, hash, cancellationToken);

    }

    public async Task UpdateUserRoleAsync(int id, string roleName, CancellationToken cancellationToken)
    {
        await _store.SetUserRoleAsync(id, roleName, cancellationToken);
    }
}
