using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace AutoTest.Infrastructure.Hubs;

/// <summary>
/// SignalR 用户标识提供器：从 JWT claim 中提取用户 ID，以支持按用户分组推送。
/// </summary>
public sealed class ClaimUserIdProvider : IUserIdProvider
{
    /// <summary>
    /// 获取当前连接的用户 ID（优先使用 sub，其次使用 NameIdentifier）。
    /// </summary>
    /// <param name="connection">Hub 连接上下文。</param>
    /// <returns>用户 ID；未认证则返回 null。</returns>
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirstValue("sub") ?? connection.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
