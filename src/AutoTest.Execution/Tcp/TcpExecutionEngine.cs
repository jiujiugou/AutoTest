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
        bool sequenceCorrect = true;
        string errorMessage = "";

        var sw = new Stopwatch();
        sw.Start();

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(target.Timeout > 0 ? target.Timeout : 30));
            using var client = new TcpClient();

            _logger?.LogInformation("Connecting to {Host}:{Port}...", target.Host, target.Port);

            // Measure connection time
            var connectSw = Stopwatch.StartNew();
            await client.ConnectAsync(target.Host, target.Port, cts.Token);
            connectSw.Stop();

            connected = true;
            latencyMs = connectSw.Elapsed.TotalMilliseconds; // Base latency is connection time

            if (target.Messages != null && target.Messages.Count > 0)
            {
                var stream = client.GetStream();
                var receivedParts = new List<string>();

                foreach (var msg in target.Messages)
                {
                    var data = Encoding.UTF8.GetBytes(msg);
                    var msgSw = Stopwatch.StartNew();

                    await stream.WriteAsync(data, cts.Token);
                    await stream.FlushAsync(cts.Token);

                    var buffer = new byte[4096];
                    // Wait for at least some response data
                    int bytesRead = await stream.ReadAsync(buffer, cts.Token);
                    msgSw.Stop();

                    // Update latency to be the average or just the last message latency
                    latencyMs = msgSw.Elapsed.TotalMilliseconds;

                    string received = Encoding.UTF8.GetString(buffer.AsSpan(0, bytesRead));
                    response += received;
                    receivedParts.Add(received);
                }

                for (int i = 0; i < target.Messages.Count; i++)
                {
                    if (i >= receivedParts.Count || !receivedParts[i].Contains(target.Messages[i]))
                    {
                        sequenceCorrect = false;
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            errorMessage = $"TCP Execution timed out after {target.Timeout} seconds.";
            _logger?.LogWarning("TCP execution timed out for {Host}:{Port}", target.Host, target.Port);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            _logger?.LogError(ex, "TCP execution failed for {Host}:{Port}", target.Host, target.Port);
        }
        finally
        {
            sw.Stop();
        }

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
