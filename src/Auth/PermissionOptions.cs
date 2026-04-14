namespace Auth;

/// <summary>
/// 全局权限系统配置项
/// </summary>
public sealed class PermissionOptions
{
    /// <summary>
    /// 策略名前缀，形如 "perm:monitor.create"
    /// </summary>
    public string PolicyPrefix { get; set; } = "perm:";

    /// <summary>
    /// 用于识别用户 ID 的 Claim 类型，默认 "sub"
    /// </summary>
    public string UserIdClaimType { get; set; } = "sub";

    /// <summary>
    /// 管理员角色，命中则直接通过授权
    /// </summary>
    public string AdminRole { get; set; } = "admin";

    /// <summary>
    /// 权限查询本地缓存 TTL
    /// </summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(5);
}
