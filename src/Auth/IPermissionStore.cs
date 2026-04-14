namespace Auth;

/// <summary>
/// 权限数据读取接口
/// </summary>
public interface IPermissionStore
{
    /// <summary>
    /// 判断用户是否拥有指定权限
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="permission">权限名</param>
    /// <param name="cancellationToken">取消标记</param>
    /// <returns>是否具有权限</returns>
    Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken);
}
