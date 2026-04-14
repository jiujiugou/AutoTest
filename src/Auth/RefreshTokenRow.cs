namespace Auth;

/// <summary>
/// RefreshToken 数据行
/// </summary>
public sealed class RefreshTokenRow
{
    /// <summary>主键</summary>
    public int Id { get; set; }
    /// <summary>用户 Id</summary>
    public int UserId { get; set; }
    /// <summary>Refresh token 字符串</summary>
    public string Token { get; set; } = "";
    /// <summary>过期时间（UTC）</summary>
    public DateTime ExpireAt { get; set; }
    /// <summary>是否已吊销</summary>
    public bool Revoked { get; set; }
    /// <summary>创建时间（UTC）</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>被旋转为的新 token 值</summary>
    public string? ReplacedByToken { get; set; }
}
