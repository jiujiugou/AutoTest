using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using AutoTest.Core;
using AutoTest.Core.Execution;
using AutoTest.Core.Target;
using Microsoft.Extensions.Logging;

namespace AutoTest.Execution.Tcp;

public class TcpExecutionEngine : IExecutionEngine
{
    private readonly ILogger<TcpExecutionEngine>? _logger;
    public TcpExecutionEngine(ILogger<TcpExecutionEngine>? logger = null)
    {
        _logger = logger;
    }
    public bool CanExecute(MonitorTarget target)
    {
        return target is TcpTarget;
    }

    public Task<ExecutionResult> ExecuteAsync(MonitorTarget target)
    {
        if (target is not TcpTarget tcpTarget)
            throw new ArgumentException("Invalid target type. Expected TcpTarget.", nameof(target));
        return ExecuteAsync(tcpTarget);
    }
    public async Task<ExecutionResult> ExecuteAsync(TcpTarget target)
    {
        bool connected = false;
        string response = "";
        double latencyMs = 0;
        bool sequenceCorrect = true; // 顺序检查
        string errorMessage = "";

        try
        {
            using var client = new TcpClient();
            var sw = new Stopwatch();

            _logger?.LogInformation("Connecting to {Host}:{Port}...", target.Host, target.Port);
            await client.ConnectAsync(target.Host, target.Port);
            connected = true;

            if (target.Messages.Count > 0)
            {
                var stream = client.GetStream();
                var receivedParts = new List<string>(); // 用于顺序检查

                foreach (var msg in target.Messages)
                {
                    var data = Encoding.UTF8.GetBytes(msg);
                    sw.Restart();
                    await stream.WriteAsync(data, 0, data.Length);
                    await stream.FlushAsync();

                    var buffer = new byte[4096];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    latencyMs = sw.Elapsed.TotalMilliseconds;

                    string received = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    response += received;
                    receivedParts.Add(received);
                }

                // 简单顺序检查逻辑
                for (int i = 0; i < target.Messages.Count; i++)
                {
                    // 假设响应中包含发送的消息作为标识
                    if (i >= receivedParts.Count || !receivedParts[i].Contains(target.Messages[i]))
                    {
                        sequenceCorrect = false;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            _logger?.LogError(ex, "TCP execution failed");
        }

        // 构造 TcpExecutionResult
        var result = new TcpExecutionResult(
            connected: connected,
            response: response,
            latencyMs: latencyMs,
            success: connected && string.IsNullOrEmpty(errorMessage),
            sequenceCorrect: sequenceCorrect,
            message: errorMessage
        );
        return result;
    }
}
