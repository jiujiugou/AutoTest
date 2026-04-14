namespace AutoTest.Application.Dto;

public sealed record DashboardStatsDto(
    int MonitorTotal,
    int Running,
    int ExecTotal,
    int ExecSuccess,
    int ExecFail,
    int AvgTime);

public sealed record DashboardSlowApiItemDto(string Api, int Time);
public sealed record DashboardFailApiItemDto(string Api, int Count);
public sealed record DashboardRecentFailItemDto(Guid Id, string Api, string Error, string Time);
public sealed record DashboardRecordItemDto(Guid Id, string Api, string Status, int Time, string Date);

public sealed record DashboardResponseDto(
    DashboardStatsDto Stats,
    IEnumerable<DashboardSlowApiItemDto> SlowApis,
    IEnumerable<DashboardFailApiItemDto> FailApis,
    IEnumerable<DashboardRecentFailItemDto> RecentFails,
    IEnumerable<DashboardRecordItemDto> Records);

