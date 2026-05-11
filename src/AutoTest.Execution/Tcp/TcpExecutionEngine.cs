using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
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

    public bool CanExecute(MonitorTarget target) => target is TcpTarget;

    public Task<ExecutionResult> ExecuteAsync(MonitorTarget target, CancellationToken ct = default)
    {
        if (target is not TcpTarget tcp)
            throw new ArgumentException("Expected TcpTarget", nameof(target));
        return ExecuteAsync(tcp, ct);
    }

    public async Task<ExecutionResult> ExecuteAsync(TcpTarget target, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        double connectLatency = 0;
        var responses = new List<string>();
        bool connected = false;

        try
        {
            (TcpClient client, Stream stream, double connMs) = await ConnectWithRetryAsync(target, ct);
            using var _ = client;
            using var __ = stream;
            connected = true;
            connectLatency = connMs;

            if (target.Messages.Count > 0)
            {
                foreach (var msg in target.Messages)
                {
                    var data = Encoding.UTF8.GetBytes(msg);

                    using (var cts = new CancellationTokenSource(target.WriteTimeoutMs))
                    {
                        await stream.WriteAsync(data, cts.Token);
                        await stream.FlushAsync(cts.Token);
                    }

                    var resp = await ReadToEndAsync(stream, target.ReadTimeoutMs);
                    responses.Add(Encoding.UTF8.GetString(resp));
                }
            }

            sw.Stop();
            return new TcpExecutionResult(
                connected: true,
                response: string.Join("\n", responses),
                latencyMs: sw.Elapsed.TotalMilliseconds,
                connectLatencyMs: connectLatency,
                success: true,
                responses: responses,
                message: "");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.LogError(ex, "TCP {Host}:{Port} failed", target.Host, target.Port);
            return new TcpExecutionResult(
                connected: connected,
                response: string.Join("\n", responses),
                latencyMs: sw.Elapsed.TotalMilliseconds,
                connectLatencyMs: connectLatency,
                success: false,
                responses: responses,
                message: ex.Message);
        }
    }

    private async Task<(TcpClient Client, Stream Stream, double ConnectLatency)> ConnectWithRetryAsync(TcpTarget t, CancellationToken ct = default)
    {
        var maxAttempts = t.EnableRetry ? t.RetryCount + 1 : 1;
        Exception? lastEx = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var connectSw = Stopwatch.StartNew();
                var client = new TcpClient();

                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    timeoutCts.CancelAfter(t.ConnectTimeoutMs);
                    await client.ConnectAsync(t.Host, t.Port, timeoutCts.Token);
                }

                Stream stream = client.GetStream();

                if (t.UseTls)
                {
                    var ssl = new SslStream(stream, leaveInnerStreamOpen: false,
                        userCertificateValidationCallback:
                            t.IgnoreSslErrors ? (_, _, _, _) => true : null!);

                    using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        timeoutCts.CancelAfter(t.ConnectTimeoutMs);
                        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                        {
                            TargetHost = t.Host,
                            CertificateRevocationCheckMode = t.IgnoreSslErrors
                                ? X509RevocationMode.NoCheck
                                : X509RevocationMode.Online
                        }, timeoutCts.Token);
                    }

                    connectSw.Stop();
                    _logger?.LogDebug("TLS handshake OK {Host}:{Port}", t.Host, t.Port);
                    return (client, ssl, connectSw.Elapsed.TotalMilliseconds);
                }

                connectSw.Stop();
                _logger?.LogDebug("TCP connected {Host}:{Port}", t.Host, t.Port);
                return (client, stream, connectSw.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastEx = ex;
                _logger?.LogWarning(ex, "TCP attempt {Attempt}/{Max} failed {Host}:{Port}",
                    attempt, maxAttempts, t.Host, t.Port);
                await Task.Delay(t.RetryDelayMs, ct);
            }
        }

        throw lastEx!;
    }

    private static async Task<byte[]> ReadToEndAsync(Stream stream, int timeoutMs)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        using var cts = new CancellationTokenSource(timeoutMs);

        while (true)
        {
            int n;
            try
            {
                n = await stream.ReadAsync(buffer, cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (n == 0) break;
            ms.Write(buffer, 0, n);
        }

        return ms.ToArray();
    }
}
