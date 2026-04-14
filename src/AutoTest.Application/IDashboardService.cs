namespace AutoTest.Application;

/// <summary>
/// 仪表盘应用服务接口。
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// 获取指定时间范围的仪表盘数据。
    /// </summary>
    /// <param name="range">时间范围标识（如 24h/7d 等）。</param>
    Task<Dto.DashboardResponseDto> GetAsync(string range = "24h");
}
