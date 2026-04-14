namespace Auth;

/// <summary>
/// 密码哈希与校验接口
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// 生成密码哈希
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <returns>哈希字符串（含算法标记与盐）</returns>
    string Hash(string password);
    /// <summary>
    /// 校验密码是否匹配哈希
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <param name="passwordHash">存储的哈希</param>
    /// <returns>是否匹配</returns>
    bool Verify(string password, string passwordHash);
}
