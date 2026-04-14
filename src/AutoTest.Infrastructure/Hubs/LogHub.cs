using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AutoTest.Infrastructure.Hubs;

/// <summary>
/// 日志实时推送 Hub：用于向前端广播增量日志（由 <see cref="AutoTest.Infrastructure.LogTailHostedService"/> 推送）。
/// </summary>
[Authorize]
public sealed class LogHub : Hub
{
}
