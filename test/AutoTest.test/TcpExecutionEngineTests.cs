using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using AutoTest.Core.Target;
using AutoTest.Execution.Tcp;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutoTest.Tests.Execution.Tcp;

public class TcpExecutionEngineTests
{
    // ========================
    // 端口连通性
    // ========================

    [Fact]
    public async Task ExecuteAsync_ShouldConnect_WhenPortIsOpen()
    {
        await using var server = await TcpEchoServer.StartAsync();
        var engine = new TcpExecutionEngine(NullLogger<TcpExecutionEngine>.Instance);
        var target = Target(server.Port);

        var result = (TcpExecutionResult)await engine.ExecuteAsync(target);

        result.Connected.Should().BeTrue();
        result.IsExecutionSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFail_WhenPortIsClosed()
    {
        var closedPort = GetFreeTcpPort();
        var engine = new TcpExecutionEngine(NullLogger<TcpExecutionEngine>.Instance);
        var target = Target(closedPort);

        var result = (TcpExecutionResult)await engine.ExecuteAsync(target);

        result.Connected.Should().BeFalse();
        result.IsExecutionSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFail_WhenHostIsUnreachable()
    {
        var engine = new TcpExecutionEngine(NullLogger<TcpExecutionEngine>.Instance);
        var target = Target(80, host: "203.0.113.1", connectTimeoutMs: 2000);

        var result = (TcpExecutionResult)await engine.ExecuteAsync(target);

        result.Connected.Should().BeFalse();
        result.IsExecutionSuccess.Should().BeFalse();
    }

    // ========================
    // TLS 握手验证
    // ========================

    [Fact]
    public async Task ExecuteAsync_ShouldEstablishTls_WhenUseTlsIsTrue()
    {
        await using var server = await TcpEchoServer.StartAsync(useTls: true);
        var engine = new TcpExecutionEngine(NullLogger<TcpExecutionEngine>.Instance);
        var target = Target(server.Port, useTls: true, ignoreSslErrors: true);

        var result = (TcpExecutionResult)await engine.ExecuteAsync(target);

        result.Connected.Should().BeTrue();
        result.IsExecutionSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFail_WhenTlsRequiredButServerIsPlain()
    {
        await using var server = await TcpEchoServer.StartAsync(useTls: false);
        var engine = new TcpExecutionEngine(NullLogger<TcpExecutionEngine>.Instance);
        var target = Target(server.Port, useTls: true, ignoreSslErrors: true, enableRetry: false);

        var result = (TcpExecutionResult)await engine.ExecuteAsync(target);

        result.IsExecutionSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFail_WhenServerCertNotTrusted_AndIgnoreSslErrorsIsFalse()
    {
        await using var server = await TcpEchoServer.StartAsync(useTls: true);
        var engine = new TcpExecutionEngine(NullLogger<TcpExecutionEngine>.Instance);
        var target = Target(server.Port, useTls: true, ignoreSslErrors: false, enableRetry: false);

        var result = (TcpExecutionResult)await engine.ExecuteAsync(target);

        result.IsExecutionSuccess.Should().BeFalse();
    }

    // ========================
    // 发文本收文本
    // ========================

    [Fact]
    public async Task ExecuteAsync_ShouldEchoMessage_WhenServerEchoes()
    {
        await using var server = await TcpEchoServer.StartAsync();
        var engine = new TcpExecutionEngine(NullLogger<TcpExecutionEngine>.Instance);
        var target = Target(server.Port, messages: ["PING"]);

        var result = (TcpExecutionResult)await engine.ExecuteAsync(target);

        result.Connected.Should().BeTrue();
        result.Response.Should().Be("PING");
        result.Responses.Should().ContainSingle().Which.Should().Be("PING");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnMultipleResponses_WhenMultipleMessages()
    {
        await using var server = await TcpEchoServer.StartAsync();
        var engine = new TcpExecutionEngine(NullLogger<TcpExecutionEngine>.Instance);
        var target = Target(server.Port, messages: ["hello", "world"]);

        var result = (TcpExecutionResult)await engine.ExecuteAsync(target);

        result.Responses.Should().HaveCount(2);
        result.Responses[0].Should().Be("hello");
        result.Responses[1].Should().Be("world");
        result.Response.Should().Contain("hello").And.Contain("world");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldConnectOnly_WhenNoMessages()
    {
        await using var server = await TcpEchoServer.StartAsync();
        var engine = new TcpExecutionEngine(NullLogger<TcpExecutionEngine>.Instance);
        var target = Target(server.Port);

        var result = (TcpExecutionResult)await engine.ExecuteAsync(target);

        result.Connected.Should().BeTrue();
        result.IsExecutionSuccess.Should().BeTrue();
        result.Responses.Should().BeEmpty();
        result.Response.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldEchoMessage_OverTls()
    {
        await using var server = await TcpEchoServer.StartAsync(useTls: true);
        var engine = new TcpExecutionEngine(NullLogger<TcpExecutionEngine>.Instance);
        var target = Target(server.Port, messages: ["secure ping"], useTls: true, ignoreSslErrors: true);

        var result = (TcpExecutionResult)await engine.ExecuteAsync(target);

        result.Responses.Should().ContainSingle().Which.Should().Be("secure ping");
    }

    // ========================
    // 延迟测量
    // ========================

    [Fact]
    public async Task ExecuteAsync_ShouldMeasureLatency()
    {
        await using var server = await TcpEchoServer.StartAsync();
        var engine = new TcpExecutionEngine(NullLogger<TcpExecutionEngine>.Instance);
        var target = Target(server.Port, messages: ["ping"]);

        var result = (TcpExecutionResult)await engine.ExecuteAsync(target);

        result.LatencyMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMeasureConnectLatency()
    {
        await using var server = await TcpEchoServer.StartAsync();
        var engine = new TcpExecutionEngine(NullLogger<TcpExecutionEngine>.Instance);
        var target = Target(server.Port, messages: ["ping"]);

        var result = (TcpExecutionResult)await engine.ExecuteAsync(target);

        result.ConnectLatencyMs.Should().BeGreaterThan(0);
        result.ConnectLatencyMs.Should().BeLessThanOrEqualTo(result.LatencyMs);
    }

    // ========================
    // 重试
    // ========================

    [Fact]
    public async Task ExecuteAsync_ShouldRetry_WhenInitialConnectionFails()
    {
        var port = GetFreeTcpPort();

        _ = Task.Run(async () =>
        {
            await Task.Delay(300);
            await using var server = await TcpEchoServer.StartAsync(port: port);
            await Task.Delay(2000);
        });

        await Task.Delay(100);

        var engine = new TcpExecutionEngine(NullLogger<TcpExecutionEngine>.Instance);
        var target = Target(port, enableRetry: true, retryCount: 3, retryDelayMs: 200);

        var result = (TcpExecutionResult)await engine.ExecuteAsync(target);

        result.Connected.Should().BeTrue();
        result.IsExecutionSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotRetry_WhenEnableRetryIsFalse()
    {
        var closedPort = GetFreeTcpPort();
        var engine = new TcpExecutionEngine(NullLogger<TcpExecutionEngine>.Instance);
        var target = Target(closedPort, enableRetry: false, connectTimeoutMs: 2000);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = (TcpExecutionResult)await engine.ExecuteAsync(target);
        sw.Stop();

        result.Connected.Should().BeFalse();
        sw.Elapsed.TotalMilliseconds.Should().BeLessThan(4000);
    }

    // ========================
    // CanExecute
    // ========================

    [Fact]
    public void CanExecute_ShouldReturnTrue_ForTcpTarget()
    {
        var engine = new TcpExecutionEngine();
        var target = Target(80);

        engine.CanExecute(target).Should().BeTrue();
    }

    [Fact]
    public void CanExecute_ShouldReturnFalse_ForOtherTarget()
    {
        var engine = new TcpExecutionEngine();
        var otherTarget = new AutoTest.Core.Target.Http.HttpTarget("http://example.com", AutoTest.Core.http.RequestMethod.Get);

        engine.CanExecute(otherTarget).Should().BeFalse();
    }

    // ========================
    // 辅助 — Target 工厂
    // ========================

    private static TcpTarget Target(
        int port,
        string host = "127.0.0.1",
        List<string>? messages = null,
        bool useTls = false,
        bool ignoreSslErrors = false,
        int connectTimeoutMs = 15000,
        int readTimeoutMs = 30000,
        int writeTimeoutMs = 10000,
        bool enableRetry = false,
        int retryCount = 2,
        int retryDelayMs = 500)
    {
        return new TcpTarget(host, port,
            messages: messages,
            useTls: useTls,
            ignoreSslErrors: ignoreSslErrors,
            connectTimeoutMs: connectTimeoutMs,
            readTimeoutMs: readTimeoutMs,
            writeTimeoutMs: writeTimeoutMs,
            enableRetry: enableRetry,
            retryCount: retryCount,
            retryDelayMs: retryDelayMs);
    }

    // ========================
    // 辅助 — 端口获取
    // ========================

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    // ========================
    // TCP Echo Server 测试辅助类
    // ========================

    private sealed class TcpEchoServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _acceptTask;
        private readonly X509Certificate2? _certificate;

        public int Port { get; }
        public bool UseTls { get; }

        private TcpEchoServer(TcpListener listener, int port, bool useTls, X509Certificate2? certificate)
        {
            _listener = listener;
            Port = port;
            UseTls = useTls;
            _certificate = certificate;
            _acceptTask = AcceptLoopAsync(_cts.Token);
        }

        public static Task<TcpEchoServer> StartAsync(bool useTls = false, int? port = null)
        {
            var cert = useTls ? CreateSelfSignedCertificate() : null;
            var listener = new TcpListener(IPAddress.Loopback, port ?? 0);
            listener.Start();
            var actualPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            var server = new TcpEchoServer(listener, actualPort, useTls, cert);
            return Task.FromResult(server);
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(ct);
                    _ = HandleClientAsync(client);
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using var _ = client;
                Stream stream = client.GetStream();

                if (UseTls && _certificate != null)
                {
                    var ssl = new SslStream(stream, leaveInnerStreamOpen: false);
                    await ssl.AuthenticateAsServerAsync(_certificate, clientCertificateRequired: false,
                        checkCertificateRevocation: false);
                    stream = ssl;
                }

                using var __ = stream;
                var buffer = new byte[8192];

                while (true)
                {
                    int n = await stream.ReadAsync(buffer);
                    if (n == 0) break;
                    await stream.WriteAsync(buffer.AsMemory(0, n));
                    await stream.FlushAsync();
                }
            }
            catch { }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Stop();
            try { await _acceptTask; } catch { }
            _cts.Dispose();
            _certificate?.Dispose();
        }

        private static X509Certificate2 CreateSelfSignedCertificate()
        {
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddIpAddress(IPAddress.Loopback);
            sanBuilder.AddDnsName("localhost");
            req.CertificateExtensions.Add(sanBuilder.Build());

            var cert = req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(7));
            return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), password: null);
        }
    }
}
