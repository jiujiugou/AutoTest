using AutoTest.Application;
using AutoTest.Application.Dto;
using Dapper;

namespace AutoTest.Infrastructure;

/// <summary>
/// 仪表盘查询服务：通过聚合 SQL 统计监控数量、运行中数量、成功率、耗时排行等指标。
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// 初始化 <see cref="DashboardService"/>。
    /// </summary>
    /// <param name="unitOfWork">工作单元，用于在同一事务上下文中执行聚合查询。</param>
    public DashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// 获取仪表盘数据。
    /// </summary>
    /// <param name="range">时间窗口：1h / 24h / 7d，默认 24h。</param>
    /// <returns>仪表盘聚合数据。</returns>
    public async Task<DashboardResponseDto> GetAsync(string range = "24h")
    {
        var now = DateTime.UtcNow;
        var from = GetFromUtc(now, range);

        DashboardResponseDto? response = null;

        await _unitOfWork.ExecuteAsync(async tx =>
        {
            var conn = tx.Connection ?? throw new InvalidOperationException("No active connection");

            var monitorCountsSql = """
                                  SELECT
                                      COUNT(1) AS MonitorTotal,
                                      COALESCE(SUM(CASE WHEN Status = @Running THEN 1 ELSE 0 END), 0) AS Running
                                  FROM Monitor
                                  """;

            var monitorCounts = await conn.QuerySingleAsync<(int MonitorTotal, int Running)>(
                monitorCountsSql,
                new { Running = 1 },
                tx);

            var execStatsSql = """
                              SELECT
                                  s.ExecTotal AS ExecTotal,
                                  s.ExecSuccess AS ExecSuccess,
                                  s.ExecFail AS ExecFail,
                                  CAST(
                                      CASE
                                          WHEN s.AvgMs > 2147483647 THEN 2147483647
                                          WHEN s.AvgMs < 0 THEN 0
                                          ELSE s.AvgMs
                                      END AS int
                                  ) AS AvgTime
                              FROM (
                                  SELECT
                                      COUNT(1) AS ExecTotal,
                                      ISNULL(SUM(CASE WHEN IsExecutionSuccess = 1 THEN 1 ELSE 0 END), 0) AS ExecSuccess,
                                      ISNULL(SUM(CASE WHEN IsExecutionSuccess = 0 THEN 1 ELSE 0 END), 0) AS ExecFail,
                                      CAST(
                                          ISNULL(
                                              AVG(
                                                  CASE
                                                      WHEN FinishedAt IS NULL OR FinishedAt < StartedAt THEN NULL
                                                      ELSE CONVERT(decimal(38, 0), DATEDIFF_BIG(MILLISECOND, StartedAt, FinishedAt))
                                                  END
                                              ),
                                              0
                                          ) AS bigint
                                      ) AS AvgMs
                                  FROM ExecutionRecord
                                  WHERE StartedAt >= @FromUtc
                              ) s
                              """;

            var execStats = await conn.QuerySingleAsync<(int ExecTotal, int ExecSuccess, int ExecFail, int AvgTime)>(
                execStatsSql,
                new { FromUtc = from },
                tx);

            var slowSql = """
                          SELECT TOP 5
                              COALESCE(NULLIF(LTRIM(RTRIM(m.Name)), ''), CONVERT(varchar(36), d.MonitorId)) AS Api,
                              CAST(
                                  CASE
                                      WHEN d.AvgMs > 2147483647 THEN 2147483647
                                      WHEN d.AvgMs < 0 THEN 0
                                      ELSE d.AvgMs
                                  END AS int
                              ) AS Time
                          FROM (
                              SELECT
                                  MonitorId,
                                  CAST(
                                      AVG(CONVERT(decimal(38, 0), DATEDIFF_BIG(MILLISECOND, StartedAt, FinishedAt))) AS bigint
                                  ) AS AvgMs
                              FROM ExecutionRecord
                              WHERE StartedAt >= @FromUtc AND FinishedAt IS NOT NULL AND FinishedAt >= StartedAt
                              GROUP BY MonitorId
                          ) d
                          LEFT JOIN Monitor m ON m.Id = d.MonitorId
                          ORDER BY d.AvgMs DESC
                          """;

            var slowApis = (await conn.QueryAsync<DashboardSlowApiItemDto>(slowSql, new { FromUtc = from }, tx)).ToList();

            var failTopSql = """
                             SELECT TOP 5
                                 COALESCE(NULLIF(LTRIM(RTRIM(m.Name)), ''), CONVERT(varchar(36), e.MonitorId)) AS Api,
                                 COUNT(1) AS Count
                             FROM ExecutionRecord e
                             LEFT JOIN Monitor m ON m.Id = e.MonitorId
                             WHERE e.StartedAt >= @FromUtc AND e.IsExecutionSuccess = 0
                             GROUP BY COALESCE(NULLIF(LTRIM(RTRIM(m.Name)), ''), CONVERT(varchar(36), e.MonitorId))
                             ORDER BY Count DESC
                             """;

            var failApis = (await conn.QueryAsync<DashboardFailApiItemDto>(failTopSql, new { FromUtc = from }, tx)).ToList();

            var recentFailsSql = """
                                 SELECT TOP 5
                                     e.Id AS Id,
                                     COALESCE(NULLIF(LTRIM(RTRIM(m.Name)), ''), CONVERT(varchar(36), e.MonitorId)) AS Api,
                                     COALESCE(NULLIF(LTRIM(RTRIM(e.ErrorMessage)), ''), '(无错误信息)') AS Error,
                                     CONVERT(varchar(8), e.StartedAt, 108) AS Time
                                 FROM ExecutionRecord e
                                 LEFT JOIN Monitor m ON m.Id = e.MonitorId
                                 WHERE e.StartedAt >= @FromUtc AND e.IsExecutionSuccess = 0
                                 ORDER BY e.StartedAt DESC
                                 """;

            var recentFails = (await conn.QueryAsync<DashboardRecentFailItemDto>(recentFailsSql, new { FromUtc = from }, tx)).ToList();

            var recordsSql = """
                             SELECT TOP 20
                                 e.Id AS Id,
                                 COALESCE(NULLIF(LTRIM(RTRIM(m.Name)), ''), CONVERT(varchar(36), e.MonitorId)) AS Api,
                                 CASE WHEN e.IsExecutionSuccess = 1 THEN 'success' ELSE 'fail' END AS Status,
                                 CASE
                                     WHEN e.FinishedAt IS NULL OR e.FinishedAt < e.StartedAt THEN 0
                                     WHEN DATEDIFF_BIG(MILLISECOND, e.StartedAt, e.FinishedAt) > 2147483647 THEN 2147483647
                                     WHEN DATEDIFF_BIG(MILLISECOND, e.StartedAt, e.FinishedAt) < 0 THEN 0
                                     ELSE CAST(DATEDIFF_BIG(MILLISECOND, e.StartedAt, e.FinishedAt) AS int)
                                 END AS Time,
                                 CONVERT(varchar(19), e.StartedAt, 120) AS Date
                             FROM ExecutionRecord e
                             LEFT JOIN Monitor m ON m.Id = e.MonitorId
                             WHERE e.StartedAt >= @FromUtc
                             ORDER BY e.StartedAt DESC
                             """;

            var records = (await conn.QueryAsync<DashboardRecordItemDto>(recordsSql, new { FromUtc = from }, tx)).ToList();

            var stats = new DashboardStatsDto(
                monitorCounts.MonitorTotal,
                monitorCounts.Running,
                execStats.ExecTotal,
                execStats.ExecSuccess,
                execStats.ExecFail,
                execStats.AvgTime);

            response = new DashboardResponseDto(stats, slowApis, failApis, recentFails, records);
        });

        return response ?? new DashboardResponseDto(
            new DashboardStatsDto(0, 0, 0, 0, 0, 0),
            Array.Empty<DashboardSlowApiItemDto>(),
            Array.Empty<DashboardFailApiItemDto>(),
            Array.Empty<DashboardRecentFailItemDto>(),
            Array.Empty<DashboardRecordItemDto>());
    }

    private static DateTime GetFromUtc(DateTime nowUtc, string range)
    {
        return range switch
        {
            "1h" => nowUtc.AddHours(-1),
            "24h" => nowUtc.AddHours(-24),
            "7d" => nowUtc.AddDays(-7),
            _ => nowUtc.AddHours(-24)
        };
    }
}
