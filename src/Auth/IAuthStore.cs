using Auth.RBAC;

namespace Auth;

/// <summary>
/// 认证所需的数据访问接口
/// </summary>
public interface IAuthStore
{
    /// <summary>用户总数（用于是否允许 Bootstrap）</summary>
    Task<int> CountUsersAsync(CancellationToken cancellationToken);
    /// <summary>按用户名查询用户</summary>
    Task<AuthUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken);
    /// <summary>按 Id 查询用户</summary>
    Task<AuthUser?> GetByIdAsync(int id, CancellationToken cancellationToken);
    /// <summary>创建用户，返回新 Id</summary>
    Task<int> CreateUserAsync(string username, string passwordHash, string role, DateTime createdAt, CancellationToken cancellationToken);

    /// <summary>查询刷新令牌</summary>
    Task<RefreshTokenRow?> GetRefreshTokenAsync(string token, CancellationToken cancellationToken);
    /// <summary>新增刷新令牌</summary>
    Task AddRefreshTokenAsync(int userId, string token, DateTime expireAt, DateTime createdAt, CancellationToken cancellationToken);
    /// <summary>吊销刷新令牌，并记录被替换的 token（旋转）</summary>
    Task RevokeRefreshTokenAsync(string token, string? replacedByToken, CancellationToken cancellationToken);
}
