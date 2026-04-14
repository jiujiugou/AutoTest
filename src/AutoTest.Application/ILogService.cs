using AutoTest.Application.Dto;

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
}
