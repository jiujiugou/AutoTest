namespace Auth;

/// <summary>
/// 访问令牌签发接口
/// </summary>
public interface ITokenIssuer
{
    /// <summary>
    /// 生成访问令牌
    /// </summary>
    /// <param name="subject">主体（通常为用户名或用户 Id）</param>
    /// <param name="role">角色</param>
    /// <returns>JWT 等形式的访问令牌</returns>
    string GenerateAccessToken(string subject, string role);
}
