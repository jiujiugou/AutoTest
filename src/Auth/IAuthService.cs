namespace Auth;

/// <summary>
/// 认证服务接口：登录/刷新/登出/初始化管理员
/// </summary>
public interface IAuthService
{
    /// <summary>用户名密码登录，返回访问令牌与刷新令牌</summary>
    Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken);
    /// <summary>使用刷新令牌获取新的访问令牌与刷新令牌（旋转）</summary>
    Task<LoginResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    /// <summary>登出：吊销刷新令牌</summary>
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);
    /// <summary>首次启动初始化管理员账号（仅当用户数为 0）</summary>
    Task BootstrapAdminAsync(string username, string password, CancellationToken cancellationToken);
    /// <summary>增加用户</summary>
    Task AddUserAsync(string username, string password, CancellationToken cancellationToken);
    /// <summary>管理员创建用户（无用户数限制检查）</summary>
    Task<int> CreateUserAsync(string username, string password, string role, CancellationToken cancellationToken);
    /// <summary>修改用户密码/// </summary>
    Task UpdateUserPasswordAsync(int id, string password, CancellationToken cancellationToken);
    /// <summary>修改用户角色/// </summary>
    Task UpdateUserRoleAsync(int id, string roleName, CancellationToken cancellationToken);
    /// <summary>修改用户基本信息</summary>
    Task UpdateUserProfileAsync(int userId, string username, bool isActive, CancellationToken cancellationToken);
    /// <summary>软删除用户</summary>
    Task DeleteUserAsync(int userId, CancellationToken cancellationToken);
}
