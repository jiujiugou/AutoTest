using System.Globalization;
using System.Text;
using AutoTest.Application.Dto;
using AutoTest.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace AutoTest.Infrastructure;

/// <summary>
/// 日志尾随推送服务：周期性读取最新日志文件的增量内容，并通过 SignalR 推送到前端实时日志视图。
/// </summary>
public sealed class LogTailHostedService : BackgroundService
{
    private readonly IHostEnvironment _environment;
    private readonly IHubContext<LogHub> _hub;
    private string? _currentFilePath;
    private long _position;
    private DateTime _currentFileLastWriteUtc;
    private readonly List<string> _pendingLines = new();
    private DateTime? _pendingTimestampUtc;
    private string _pendingLevel = "INFO";

    /// <summary>
    /// 初始化 <see cref="LogTailHostedService"/>。
    /// </summary>
    /// <param name="environment">宿主环境，用于定位日志目录。</param>
    /// <param name="hub">SignalR Hub 上下文，用于广播追加的日志条目。</param>
    public LogTailHostedService(IHostEnvironment environment, IHubContext<LogHub> hub)
    {
        _environment = environment;
        _hub = hub;
    }

    /// <summary>
    /// 后台执行循环：按固定间隔读取最新日志文件并推送新增日志。
    /// </summary>
    /// <param name="stoppingToken">停止令牌。</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch
            {
            }

            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var file = GetLatestLogFile();
        if (file == null) return;

        if (_currentFilePath == null || !string.Equals(_currentFilePath, file.FullName, StringComparison.OrdinalIgnoreCase))
        {
            _currentFilePath = file.FullName;
            _position = 0;
            _pendingLines.Clear();
            _pendingTimestampUtc = null;
            _pendingLevel = "INFO";
        }

        if (_currentFileLastWriteUtc != file.LastWriteTimeUtc)
            _currentFileLastWriteUtc = file.LastWriteTimeUtc;

        using var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (_position > fs.Length) _position = 0;
        fs.Seek(_position, SeekOrigin.Begin);

        using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
        string? line;
        while ((line = await sr.ReadLineAsync(ct)) != null)
        {
            HandleLine(file.Name, line);
        }

        _position = fs.Position;
    }

    private void HandleLine(string fileName, string line)
    {
        if (TryParseHeader(line, out var tsLocal, out var level, out var message))
        {
            if (_pendingTimestampUtc != null)
            {
                var item = BuildDto(fileName, _pendingTimestampUtc.Value, _pendingLevel, _pendingLines);
                if (item != null)
                    _ = _hub.Clients.All.SendAsync("LogAppended", item);
            }

            _pendingLines.Clear();
            _pendingTimestampUtc = tsLocal.ToUniversalTime();
            _pendingLevel = level;
            _pendingLines.Add(message);
            return;
        }

        if (_pendingTimestampUtc != null)
            _pendingLines.Add(line);
    }

    private FileInfo? GetLatestLogFile()
    {
        var logDir = Path.Combine(_environment.ContentRootPath, "logs");
        if (!Directory.Exists(logDir)) return null;

        return new DirectoryInfo(logDir)
            .GetFiles("log-*.txt", SearchOption.TopDirectoryOnly)
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static LogItemDto? BuildDto(string fileName, DateTime timestampUtc, string level, IReadOnlyList<string> lines)
    {
        var text = string.Join("\n", lines).TrimEnd();
        if (string.IsNullOrWhiteSpace(text)) return null;

        var cursorRaw = $"{fileName}|{timestampUtc.Ticks}|0";
        var cursor = Convert.ToBase64String(Encoding.UTF8.GetBytes(cursorRaw));
        return new LogItemDto(
            cursor,
            timestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            level,
            "Webapi",
            text);
    }

    private static bool TryParseHeader(string line, out DateTime timestampLocal, out string level, out string message)
    {
        timestampLocal = default;
        level = "INFO";
        message = string.Empty;

        if (line.Length < 22) return false;

        var ts = line.Substring(0, 19);
        if (!DateTime.TryParseExact(ts, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out timestampLocal))
            return false;

        var rest = line.Substring(19).TrimStart();
        if (rest.StartsWith('['))
        {
            var end = rest.IndexOf(']');
            if (end > 1)
            {
                var token = rest.Substring(1, end - 1);
                level = token switch
                {
                    "INF" => "INFO",
                    "WRN" => "WARN",
                    "ERR" => "ERROR",
                    "DBG" => "DEBUG",
                    "FTL" => "FATAL",
                    _ => token
                };
                message = rest.Substring(end + 1).TrimStart();
                return true;
            }
        }

        message = rest;
        return true;
    }
}
