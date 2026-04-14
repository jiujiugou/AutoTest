using System.Globalization;
using System.Text;
using AutoTest.Application;
using AutoTest.Application.Dto;
using Microsoft.Extensions.Hosting;

namespace AutoTest.Infrastructure;

/// <summary>
/// 日志查询服务：从 Webapi 生成的日志文件中解析条目，并支持分页/过滤。
/// </summary>
public sealed class LogService : ILogService
{
    private readonly IHostEnvironment _environment;

    /// <summary>
    /// 初始化 <see cref="LogService"/>。
    /// </summary>
    /// <param name="environment">宿主环境，用于定位日志目录。</param>
    public LogService(IHostEnvironment environment)
    {
        _environment = environment;
    }

    /// <summary>
    /// 清空当前最新日志文件内容。
    /// </summary>
    public Task ClearAsync()
    {
        var file = GetLatestLogFile();
        if (file == null) return Task.CompletedTask;

        using var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        fs.SetLength(0);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 查询日志（支持 level/module/keyword 与时间范围过滤，以及基于 cursor 的向后分页）。
    /// </summary>
    /// <param name="query">查询条件。</param>
    /// <returns>分页结果。</returns>
    public Task<LogPageDto> QueryAsync(LogQueryDto query)
    {
        var take = query.Take <= 0 ? 200 : query.Take;
        if (take > 500) take = 500;

        var file = GetLatestLogFile();
        if (file == null) return Task.FromResult(new LogPageDto(Array.Empty<LogItemDto>(), null, false));

        var before = TryDecodeCursor(query.Before);

        var items = new List<LogItem>(capacity: Math.Min(take, 500));
        var currentLines = new List<string>();
        DateTime? currentTs = null;
        string currentLevel = "INFO";
        var currentStartLine = 0;

        var lineNo = 0;
        try
        {
            using var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: false);
            string? rawLine;
            while ((rawLine = sr.ReadLine()) != null)
            {
                lineNo++;
                var line = rawLine ?? string.Empty;

                if (TryParseHeader(line, out var ts, out var level, out var msg))
                {
                    if (currentTs != null)
                    {
                        var item = BuildItem(file.Name, currentTs.Value, currentLevel, currentStartLine, currentLines);
                        if (item != null) items.Add(item);
                    }

                    currentLines.Clear();
                    currentTs = ts;
                    currentLevel = level;
                    currentStartLine = lineNo;
                    currentLines.Add(msg);
                    continue;
                }

                if (currentTs != null)
                    currentLines.Add(line);
            }
        }
        catch (IOException)
        {
            return Task.FromResult(new LogPageDto(Array.Empty<LogItemDto>(), null, false));
        }

        if (currentTs != null)
        {
            var item = BuildItem(file.Name, currentTs.Value, currentLevel, currentStartLine, currentLines);
            if (item != null) items.Add(item);
        }

        IEnumerable<LogItem> filtered = items;

        if (!string.IsNullOrWhiteSpace(query.Level))
        {
            var target = query.Level.Trim().ToUpperInvariant();
            filtered = filtered.Where(x => string.Equals(x.Level, target, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Module))
        {
            var target = query.Module.Trim();
            filtered = filtered.Where(x => x.Module.Contains(target, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            filtered = filtered.Where(x => x.Message.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }

        if (query.FromUtc.HasValue)
            filtered = filtered.Where(x => x.TimestampUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue)
            filtered = filtered.Where(x => x.TimestampUtc <= query.ToUtc.Value);

        filtered = filtered
            .OrderByDescending(x => x.TimestampUtc)
            .ThenByDescending(x => x.StartLine);

        if (before != null && string.Equals(before.FileName, file.Name, StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(x =>
                x.TimestampUtc < before.TimestampUtc ||
                (x.TimestampUtc == before.TimestampUtc && x.StartLine < before.StartLine));
        }

        var page = filtered.Take(take + 1).ToList();
        var hasMore = page.Count > take;
        if (hasMore) page = page.Take(take).ToList();

        var dtoItems = page.Select(x => new LogItemDto(
            x.Cursor,
            x.TimestampLocalString,
            x.Level,
            x.Module,
            x.Message)).ToList();

        var next = dtoItems.Count > 0 ? dtoItems[^1].Cursor : null;
        return Task.FromResult(new LogPageDto(dtoItems, hasMore ? next : null, hasMore));
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

    private static LogItem? BuildItem(string fileName, DateTime timestamp, string level, int startLine, IReadOnlyList<string> lines)
    {
        var text = string.Join("\n", lines).TrimEnd();
        if (string.IsNullOrWhiteSpace(text)) return null;

        var cursor = EncodeCursor(fileName, timestamp, startLine);
        return new LogItem(
            cursor,
            timestamp,
            level,
            "Webapi",
            text,
            startLine);
    }

    private static bool TryParseHeader(string line, out DateTime timestamp, out string level, out string message)
    {
        timestamp = default;
        level = "INFO";
        message = string.Empty;

        if (line.Length < 22) return false;

        var ts = line.Substring(0, 19);
        if (!DateTime.TryParseExact(ts, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp))
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

    private static string EncodeCursor(string fileName, DateTime timestamp, int startLine)
    {
        var raw = $"{fileName}|{timestamp.ToUniversalTime().Ticks}|{startLine}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    private static DecodedCursor? TryDecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|');
            if (parts.Length != 3) return null;
            if (!long.TryParse(parts[1], out var ticks)) return null;
            if (!int.TryParse(parts[2], out var startLine)) return null;
            return new DecodedCursor(parts[0], new DateTime(ticks, DateTimeKind.Utc), startLine);
        }
        catch
        {
            return null;
        }
    }

    private sealed record DecodedCursor(string FileName, DateTime TimestampUtc, int StartLine);

    private sealed record LogItem(
        string Cursor,
        DateTime TimestampUtc,
        string Level,
        string Module,
        string Message,
        int StartLine)
    {
        public string TimestampLocalString => TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
