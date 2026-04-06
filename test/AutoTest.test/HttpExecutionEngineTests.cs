using AutoTest.Core;
using AutoTest.Core.http;
using AutoTest.Core.Target.Http;
using AutoTest.Execution.Http;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Xunit;

namespace AutoTest.Tests.Execution.Http;

public class HttpExecutionEngineTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess_AndCaptureBodyHeaders()
    {
        await using var server = await TestHttpServer.StartAsync(async ctx =>
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.Headers["X-Test"] = "abc";
            await ctx.Response.WriteAsync("ok");
        });

        var engine = new HttpExecutionEngine(new NullLogger<HttpExecutionEngine>());
        var target = new HttpTarget(server.Url("/ok").ToString(), RequestMethod.Get, null!);
        target.RetryCount = 1;

        var result = (HttpExecutionResult)await engine.ExecuteAsync(target);
        result.IsExecutionSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Body.Should().Be("ok");
        result.Headers.Should().ContainKey("X-Test");
        result.ElapsedMilliseconds.Should().NotBeNull();
        result.ElapsedMilliseconds!.Value.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSendJsonBody()
    {
        await using var server = await TestHttpServer.StartAsync(async ctx =>
        {
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsync(body);
        });

        var engine = new HttpExecutionEngine(new NullLogger<HttpExecutionEngine>());
        var body = new HttpBody(BodyType.Json, "application/json", new { a = 1 });
        var target = new HttpTarget(server.Url("/echo").ToString(), RequestMethod.Post, body);
        target.RetryCount = 1;

        var result = (HttpExecutionResult)await engine.ExecuteAsync(target);
        result.StatusCode.Should().Be(200);
        result.Body.Should().Be("{\"a\":1}");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSendFormUrlEncodedBody()
    {
        await using var server = await TestHttpServer.StartAsync(async ctx =>
        {
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsync($"{ctx.Request.ContentType}|{body}");
        });

        var engine = new HttpExecutionEngine(new NullLogger<HttpExecutionEngine>());
        var body = new HttpBody(BodyType.FormUrlEncoded, "application/x-www-form-urlencoded", new Dictionary<string, string>
        {
            ["k"] = "v",
            ["k2"] = "v 2"
        });
        var target = new HttpTarget(server.Url("/form").ToString(), RequestMethod.Post, body);
        target.RetryCount = 1;

        var result = (HttpExecutionResult)await engine.ExecuteAsync(target);
        result.StatusCode.Should().Be(200);
        result.Body.Should().Contain("application/x-www-form-urlencoded");
        result.Body.Should().Contain("k=v");
        result.Body.Should().Contain("k2=v+2");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSendRawBody_WithContentType()
    {
        await using var server = await TestHttpServer.StartAsync(async ctx =>
        {
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsync($"{ctx.Request.ContentType}|{body}");
        });

        var engine = new HttpExecutionEngine(new NullLogger<HttpExecutionEngine>());
        var body = new HttpBody(BodyType.Raw, "text/plain", "hello");
        var target = new HttpTarget(server.Url("/raw").ToString(), RequestMethod.Post, body);
        target.RetryCount = 1;

        var result = (HttpExecutionResult)await engine.ExecuteAsync(target);
        result.StatusCode.Should().Be(200);
        result.Body.Should().Contain("text/plain");
        result.Body.Should().EndWith("|hello");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAppendQueryParams()
    {
        await using var server = await TestHttpServer.StartAsync(async ctx =>
        {
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsync(ctx.Request.QueryString.Value ?? "");
        });

        var engine = new HttpExecutionEngine(new NullLogger<HttpExecutionEngine>());
        var target = new HttpTarget(server.Url("/q").ToString(), RequestMethod.Get, null!);
        target.Query!["a"] = "1";
        target.Query["b"] = "hello world";
        target.RetryCount = 1;

        var result = (HttpExecutionResult)await engine.ExecuteAsync(target);
        result.Body.Should().Contain("a=1");
        result.Body.Should().Contain("b=hello%20world");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSetBearerAuthHeader()
    {
        await using var server = await TestHttpServer.StartAsync(async ctx =>
        {
            var auth = ctx.Request.Headers.Authorization.ToString();
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsync(auth);
        });

        var engine = new HttpExecutionEngine(new NullLogger<HttpExecutionEngine>());
        var target = new HttpTarget(server.Url("/auth").ToString(), RequestMethod.Get, null!);
        target.AuthType = AuthType.Bearer;
        target.AuthToken = "t1";
        target.RetryCount = 1;

        var result = (HttpExecutionResult)await engine.ExecuteAsync(target);
        result.Body.Should().Be("Bearer t1");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSetBasicAuthHeader()
    {
        await using var server = await TestHttpServer.StartAsync(async ctx =>
        {
            var auth = ctx.Request.Headers.Authorization.ToString();
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsync(auth);
        });

        var engine = new HttpExecutionEngine(new NullLogger<HttpExecutionEngine>());
        var target = new HttpTarget(server.Url("/basic").ToString(), RequestMethod.Get, null!);
        target.AuthType = AuthType.Basic;
        target.AuthUsername = "u";
        target.AuthPassword = "p";
        target.RetryCount = 1;

        var result = (HttpExecutionResult)await engine.ExecuteAsync(target);
        result.Body.Should().StartWith("Basic ");
        var b64 = result.Body!.Substring("Basic ".Length);
        Encoding.UTF8.GetString(Convert.FromBase64String(b64)).Should().Be("u:p");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSetApiKeyHeader()
    {
        await using var server = await TestHttpServer.StartAsync(async ctx =>
        {
            var key = ctx.Request.Headers["X-Api-Key"].ToString();
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsync(key);
        });

        var engine = new HttpExecutionEngine(new NullLogger<HttpExecutionEngine>());
        var target = new HttpTarget(server.Url("/apikey").ToString(), RequestMethod.Get, null!);
        target.AuthType = AuthType.ApiKeyHeader;
        target.AuthToken = "k1";
        target.RetryCount = 1;

        var result = (HttpExecutionResult)await engine.ExecuteAsync(target);
        result.Body.Should().Be("k1");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturn4044_WhenSameUrlIsInFlight()
    {
        await using var server = await TestHttpServer.StartAsync(async ctx =>
        {
            await Task.Delay(400);
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsync("ok");
        });

        var engine = new HttpExecutionEngine(new NullLogger<HttpExecutionEngine>());
        var url = server.Url("/slow").ToString();
        var t1Target = new HttpTarget(url, RequestMethod.Get, null!);
        var t2Target = new HttpTarget(url, RequestMethod.Get, null!);
        t1Target.RetryCount = 1;
        t2Target.RetryCount = 1;

        var first = engine.ExecuteAsync(t1Target);
        await Task.Delay(30);
        var second = (HttpExecutionResult)await engine.ExecuteAsync(t2Target);

        second.StatusCode.Should().Be(4044);
        second.IsExecutionSuccess.Should().BeFalse();
        await first;
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRetry_WhenTransientFailureThenSuccess()
    {
        var port = GetFreeTcpPort();
        var url = new Uri($"http://127.0.0.1:{port}/ok");

        _ = Task.Run(async () =>
        {
            await Task.Delay(250);
            await using var server = await TestHttpServer.StartAsync(async ctx =>
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.WriteAsync("ok");
            }, port);

            await Task.Delay(800);
        });

        var engine = new HttpExecutionEngine(new NullLogger<HttpExecutionEngine>());
        var target = new HttpTarget(url.ToString(), RequestMethod.Get, null!);
        target.EnableRetry = true;
        target.RetryCount = 2;
        target.RetryDelayMs = 400;

        var result = (HttpExecutionResult)await engine.ExecuteAsync(target);
        result.StatusCode.Should().Be(200);
        result.Body.Should().Be("ok");
        result.IsExecutionSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenTimeout()
    {
        await using var server = await TestHttpServer.StartAsync(async ctx =>
        {
            await Task.Delay(1500);
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsync("late");
        });

        var engine = new HttpExecutionEngine(new NullLogger<HttpExecutionEngine>());
        var target = CreateTargetWithTimeout(server.Url("/timeout").ToString(), 1);
        target.RetryCount = 1;

        var result = (HttpExecutionResult)await engine.ExecuteAsync(target);
        result.IsExecutionSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLimitGlobalConcurrency_WhenRateLimitEnabled()
    {
        var current = 0;
        var max = 0;

        await using var server = await TestHttpServer.StartAsync(async ctx =>
        {
            var now = Interlocked.Increment(ref current);
            UpdateMax(ref max, now);
            try
            {
                await Task.Delay(250);
                ctx.Response.StatusCode = 200;
                await ctx.Response.WriteAsync("ok");
            }
            finally
            {
                Interlocked.Decrement(ref current);
            }
        });

        var engine = new HttpExecutionEngine(new NullLogger<HttpExecutionEngine>());
        var tasks = Enumerable.Range(0, 6).Select(i =>
        {
            var target = new HttpTarget(server.Url($"/p/{i}").ToString(), RequestMethod.Get, null!);
            target.EnableRateLimit = true;
            target.RetryCount = 1;
            return engine.ExecuteAsync(target);
        });

        await Task.WhenAll(tasks);
        max.Should().BeLessThanOrEqualTo(5);
    }

    private static void UpdateMax(ref int target, int value)
    {
        while (true)
        {
            var snapshot = target;
            if (value <= snapshot)
                return;
            if (Interlocked.CompareExchange(ref target, value, snapshot) == snapshot)
                return;
        }
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static HttpTarget CreateTargetWithTimeout(string url, int timeoutSeconds)
    {
        var json = JsonSerializer.Serialize(new
        {
            Url = url,
            Method = RequestMethod.Get,
            Timeout = timeoutSeconds
        });
        return JsonSerializer.Deserialize<HttpTarget>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
    }

    private sealed class TestHttpServer : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private TestHttpServer(WebApplication app, Uri baseUri)
        {
            _app = app;
            BaseUri = baseUri;
        }

        public Uri BaseUri { get; }

        public Uri Url(string path)
        {
            var p = path.StartsWith("/") ? path : "/" + path;
            return new Uri(BaseUri, p);
        }

        public static async Task<TestHttpServer> StartAsync(Func<HttpContext, Task> handler, int? port = null)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });
            builder.Services.AddRouting();
            builder.WebHost.ConfigureKestrel(options =>
            {
                if (port.HasValue)
                    options.Listen(IPAddress.Loopback, port.Value);
                else
                    options.Listen(IPAddress.Loopback, 0);
            });

            var app = builder.Build();
            app.MapFallback(handler);
            await app.StartAsync();

            var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses;
            var baseUri = new Uri(addresses.First());
            return new TestHttpServer(app, baseUri);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
