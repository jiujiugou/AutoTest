using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AutoTest.Infrastructure.Hubs;

[Authorize]
public sealed class LogHub : Hub
{
    public static class GroupNames
    {
        public const string Tail = "logs:tail";
    }

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.Tail);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupNames.Tail);
        await base.OnDisconnectedAsync(exception);
    }
}