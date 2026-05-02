using AutoTest.Application.Dto;
using AutoTest.Core.AI;

namespace AutoTest.Application;

/// <summary>
/// 日志应用服务接口。
/// </summary>
public interface ILogService
{
    /// <summary>
    /// 按条件查询日志（分页）。
    /// </summary>
    Task<LogPageDto> QueryAsync(LogQueryDto query);

    /// <summary>
    /// 清理日志。
    /// </summary>
    Task ClearAsync();

    /// <summary>
    /// 获取指定 TraceId 的 AI 错误上下文日志。
    /// </summary>
    Task<List<TraceLogEntry>> GetAiErrorContextAsync(string traceId, DateTime? errorTime = null, int windowSeconds = 30, int take = 120);
}
