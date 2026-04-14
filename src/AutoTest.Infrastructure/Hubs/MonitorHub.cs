using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace AutoTest.Infrastructure.Hubs;

/// <summary>
/// 监控状态推送 Hub：用于将监控任务的运行/完成等状态变更推送到前端。
/// </summary>
[Authorize]
public sealed class MonitorHub : Hub
{
    /// <summary>
    /// 连接建立时将当前连接加入到用户组与角色组，便于后续按组推送。
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue("sub") ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.User(userId));

        var role = Context.User?.FindFirstValue(ClaimTypes.Role);
        if (!string.IsNullOrWhiteSpace(role))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.Role(role));

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// SignalR 分组命名规则。
    /// </summary>
    public static class GroupNames
    {
        /// <summary>
        /// 用户组名。
        /// </summary>
        /// <param name="userId">用户 ID。</param>
        /// <returns>组名。</returns>
        public static string User(string userId) => $"user:{userId}";

        /// <summary>
        /// 角色组名。
        /// </summary>
        /// <param name="role">角色。</param>
        /// <returns>组名。</returns>
        public static string Role(string role) => $"role:{role}";
    }
}
