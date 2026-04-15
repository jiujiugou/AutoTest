using AutoTest.Application.Dto;
using AutoTest.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Serilog.Core;
using Serilog.Events;
using System.Globalization;
using System.Threading.Channels;

namespace AutoTest.Webapi;

public class SignalRLogSink : ILogEventSink
{
    // 静态的 ServiceProvider 引用
    private static volatile IServiceProvider? _serviceProvider;
    public static IServiceProvider? ServiceProvider
    {
        get => _serviceProvider;
        set => _serviceProvider = value;
    }

    private static IHubContext<LogHub>? _hubContext;

    // 缓冲通道
    private static readonly Channel<LogItemDto> _channel = Channel.CreateUnbounded<LogItemDto>();

    // 使用标志位，确保消费线程只启动一次
    private static int _isConsumerStarted = 0;

    public SignalRLogSink()
    {
        // 在实例构造时，尝试启动后台消费线程
        StartConsumerThread();
    }

    private static void StartConsumerThread()
    {
        // 确保整个应用程序生命周期内只启动一个消费线程
        if (Interlocked.Exchange(ref _isConsumerStarted, 1) == 1) return;

        Task.Run(async () =>
        {
            Console.WriteLine("🚀 [Sink] 后台日志推送线程已成功启动！");

            try
            {
                await foreach (var item in _channel.Reader.ReadAllAsync())
                {
                    try
                    {
                        if (_serviceProvider == null)
                        {
                            await Task.Delay(100);
                            continue;
                        }

                        if (_hubContext == null)
                        {
                            try
                            {
                                _hubContext = _serviceProvider.GetRequiredService<IHubContext<LogHub>>();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[Sink-Error] 致命错误：无法获取 IHubContext！原因: {ex.Message}");
                                await Task.Delay(1000);
                                continue;
                            }
                        }

                        Console.WriteLine($"[Sink-Push] 正在推送日志: {item.Message}");

                        var payload = new
                        {
                            cursor = item.Cursor,
                            timestamp = item.Timestamp,
                            level = item.Level,
                            module = item.Module,
                            message = item.Message
                        };

                        await _hubContext.Clients
                            .Group(LogHub.GroupNames.Tail)
                            .SendAsync("logAppended", payload);
                    }
                    catch (Exception innerEx)
                    {
                        Console.WriteLine($"[Sink-Error] 推送单条日志时发生异常: {innerEx.Message}");
                    }
                }
            }
            catch (Exception outerEx)
            {
                // 如果跑到这里，说明 Channel 挂了或者 Task 彻底崩了！
                Console.WriteLine($"💥 [Sink-Fatal] 后台消费线程彻底崩溃: {outerEx}");
            }
            finally
            {
                Console.WriteLine("🛑 [Sink] 后台消费线程已退出。");
            }
        });
    }

    public void Emit(LogEvent logEvent)
    {
        try
        {
            var level = logEvent.Level switch
            {
                LogEventLevel.Information => "INFO",
                LogEventLevel.Warning => "WARN",
                LogEventLevel.Error => "ERROR",
                LogEventLevel.Fatal => "FATAL",
                LogEventLevel.Debug => "DEBUG",
                LogEventLevel.Verbose => "TRACE",
                _ => logEvent.Level.ToString().ToUpper()
            };

            var message = logEvent.RenderMessage() +
                (logEvent.Exception != null ? $"\n{logEvent.Exception}" : "");

            var cursorRaw = $"{Guid.NewGuid()}|{logEvent.Timestamp.UtcTicks}|0";
            var cursor = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(cursorRaw));

            var dto = new LogItemDto(
                cursor,
                logEvent.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                level,
                "Webapi",
                message
            );

            // 写入通道
            if (!_channel.Writer.TryWrite(dto))
            {
                Console.WriteLine("[Sink-Error] 写入通道失败！");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Sink-Emit-Error] Emit 方法内部异常: {ex.Message}");
        }
    }
}