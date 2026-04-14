namespace Auth.RBAC;

/// <summary>
/// 用户实体
/// </summary>
public sealed class AuthUser
{
    /// <summary>用户主键</summary>
    public int Id { get; set; }

    /// <summary>用户名</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>密码哈希</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>角色名称（如 admin / user）</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>是否启用</summary>
    public bool IsActive { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>最后登录时间</summary>
    public DateTime? LastLoginAt { get; set; }
}
