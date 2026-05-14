using Auth.RBAC;
using AutoTest.Core.Auth;

namespace Auth;

/// <summary>
/// 认证与账户数据访问接口（Auth Store）
/// </summary>
public interface IAuthStore
{
    /// <summary>用户总数（用于 Bootstrap 判断）</summary>
    Task<int> CountUsersAsync(CancellationToken cancellationToken);

    /// <summary>根据用户名查询用户（登录用）</summary>
    Task<AuthUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken);

    /// <summary>根据用户ID查询用户</summary>
    Task<AuthUser?> GetByIdAsync(int id, CancellationToken cancellationToken);

    // =====================================================
    //  用户基础信息（账号/密码）
    // =====================================================

    /// <summary>
    /// 修改用户密码（必须是 Hash 后的密码）
    /// </summary>
    Task<bool> UpdatePasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken);

    /// <summary>
    /// 修改用户基本信息（如用户名、启用状态）
    /// </summary>
    Task<bool> UpdateUserProfileAsync(
        int userId,
        string username,
        bool isActive,
        CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// 更新用户最后登录时间
    /// </summary>
    Task UpdateLastLoginAsync(int userId, DateTime loginTime, CancellationToken cancellationToken);
    // =====================================================
    //  用户角色管理（RBAC 第一层）
    // =====================================================

    /// <summary>
    /// 设置用户角色（覆盖式）
    /// 一个用户可以只有一个角色（你当前设计）
    /// </summary>
    Task<bool> SetUserRoleAsync(
        int userId,
        string roleName,
        CancellationToken cancellationToken);

    // =====================================================
    //  用户权限（RBAC 第二层）
    // =====================================================

    /// <summary>
    /// 获取用户最终权限集合（角色 + 直接权限）
    /// </summary>
    Task<IReadOnlyList<string>> GetUserPermissionsAsync(
        int userId,
        CancellationToken cancellationToken);


    // =====================================================
    //  用户创建 / 删除
    // =====================================================

    Task<int> CreateUserAsync(
        string username,
        string passwordHash,
        string role,
        DateTime createdAt,
        CancellationToken cancellationToken);

    // =====================================================
    //  Refresh Token
    // =====================================================

    Task<RefreshTokenRow?> GetRefreshTokenAsync(string token, CancellationToken cancellationToken);

    Task AddRefreshTokenAsync(
        int userId,
        string token,
        DateTime expireAt,
        DateTime createdAt,
        CancellationToken cancellationToken);

    Task RevokeRefreshTokenAsync(
        string token,
        string? replacedByToken,
        CancellationToken cancellationToken);
}