using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AutoTest.Application;
using AutoTest.Tests;
using AutoTest.Core.Abstraction;
using AutoTest.Core.Execution;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace AutoTest.Tests.Integration;

public class HttpEndToEndIntegrationTests
{
    [Fact]
    public async Task Create_Run_And_GetLatestExecution_ShouldWork_ForHttpMonitor()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"autotest-it-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var dbPath = Path.Combine(tempRoot, "AutoTestDb.sqlite");
        var hangfireDbPath = Path.Combine(tempRoot, "hangfire.db");
        try
        {
            await using var server = await LocalHttpServer.StartAsync(async ctx =>
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.WriteAsync("ok");
            });

            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseContentRoot(GetWebApiContentRoot());
                builder.UseSetting("Database:Provider", "Sqlite");
                builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={dbPath};");
                builder.UseSetting("ConnectionStrings:HangfireConnection", $"Data Source={hangfireDbPath};Foreign Keys=True;");
                builder.ConfigureAppConfiguration((_, cfg) =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Database:Provider"] = "Sqlite",
                        ["ConnectionStrings:DefaultConnection"] = $"Data Source={dbPath};",
                        ["ConnectionStrings:HangfireConnection"] = $"Data Source={hangfireDbPath};Foreign Keys=True;"
                    });
                });

                builder.ConfigureServices(services =>
                {
                    while (true)
                    {
                        var hosted = services.FirstOrDefault(d =>
                            d.ServiceType == typeof(IHostedService) &&
                            d.ImplementationType?.FullName?.Contains("Hangfire", StringComparison.OrdinalIgnoreCase) == true);
                        if (hosted == null)
                            break;
                        services.Remove(hosted);
                    }

                    while (true)
                    {
                        var scheduler = services.FirstOrDefault(d => d.ServiceType == typeof(IWorkflowScheduler));
                        if (scheduler == null)
                            break;
                        services.Remove(scheduler);
                    }

                    services.AddSingleton<IWorkflowScheduler, ImmediateWorkflowScheduler>();

                    services.AddAuthentication("Test")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                });
            });

            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");

            var assertionId = Guid.NewGuid();
            var httpAssertionConfig = JsonSerializer.Serialize(new
            {
                Id = assertionId,
                Field = "StatusCode",
                Operator = "Equal",
                HeaderKey = "",
                Expected = "200"
            });

            var httpTargetConfig = JsonSerializer.Serialize(new
            {
                Method = "Get",
                Url = server.Url("/"),
                Body = (object?)null,
                Headers = new Dictionary<string, string>(),
                Query = new Dictionary<string, string>(),
                Timeout = 5
            });

            var createResp = await client.PostAsJsonAsync("/api/monitor", new
            {
                Name = "IT HTTP",
                TargetType = "HTTP",
                TargetConfig = httpTargetConfig,
                IsEnabled = true,
                Assertions = new[]
                {
                    new
                    {
                        Id = assertionId,
                        Type = "HTTP",
                        ConfigJson = httpAssertionConfig
                    }
                }
            });

            createResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var monitorId = (await createResp.Content.ReadFromJsonAsync<Guid>())!;
            monitorId.Should().NotBe(Guid.Empty);

            var runResp = await client.PostAsync($"/api/monitor/{monitorId}/run",
                new StringContent("", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"));
            runResp.StatusCode.Should().Be(HttpStatusCode.OK);

            using var latestResp = await client.GetAsync($"/api/monitor/{monitorId}/executions/latest");
            latestResp.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await latestResp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            var root = doc.RootElement;
            var record = GetProperty(root, "record");
            GetProperty(record, "isExecutionSuccess").GetBoolean().Should().BeTrue();
            GetProperty(record, "resultType").GetString().Should().Be("HTTP");

            var assertions = GetProperty(root, "assertions");
            assertions.ValueKind.Should().Be(JsonValueKind.Array);
            assertions.EnumerateArray().Should().ContainSingle();
            var a0 = assertions.EnumerateArray().First();
            GetProperty(a0, "isSuccess").GetBoolean().Should().BeTrue();
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task FailedExecution_ShouldEmitWebhookNotification()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"autotest-it-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var dbPath = Path.Combine(tempRoot, "AutoTestDb.sqlite");
        var hangfireDbPath = Path.Combine(tempRoot, "hangfire.db");
        var received = new TaskCompletionSource<JsonDocument>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            await using var webhookServer = await LocalHttpServer.StartAsync(async ctx =>
            {
                using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
                var body = await reader.ReadToEndAsync();
                received.TrySetResult(JsonDocument.Parse(body));
                ctx.Response.StatusCode = 200;
                await ctx.Response.WriteAsync("ok");
            });

            await using var server = await LocalHttpServer.StartAsync(async ctx =>
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.WriteAsync("ok");
            });

            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseContentRoot(GetWebApiContentRoot());
                builder.UseSetting("Database:Provider", "Sqlite");
                builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={dbPath};");
                builder.UseSetting("ConnectionStrings:HangfireConnection", $"Data Source={hangfireDbPath};Foreign Keys=True;");
                builder.ConfigureAppConfiguration((_, cfg) =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Database:Provider"] = "Sqlite",
                        ["ConnectionStrings:DefaultConnection"] = $"Data Source={dbPath};",
                        ["ConnectionStrings:HangfireConnection"] = $"Data Source={hangfireDbPath};Foreign Keys=True;",
                        ["Outbox:Webhook:Enabled"] = "true",
                        ["Outbox:Webhook:Url"] = webhookServer.Url("/"),
                        ["Outbox:Webhook:PollIntervalMs"] = "200",
                        ["Outbox:Webhook:BatchSize"] = "20",
                        ["Outbox:Webhook:LockSeconds"] = "30",
                        ["Outbox:Webhook:TimeoutSeconds"] = "5"
                    });
                });

                builder.ConfigureServices(services =>
                {
                    while (true)
                    {
                        var hosted = services.FirstOrDefault(d =>
                            d.ServiceType == typeof(IHostedService) &&
                            d.ImplementationType?.FullName?.Contains("Hangfire", StringComparison.OrdinalIgnoreCase) == true);
                        if (hosted == null)
                            break;
                        services.Remove(hosted);
                    }

                    while (true)
                    {
                        var scheduler = services.FirstOrDefault(d => d.ServiceType == typeof(IWorkflowScheduler));
                        if (scheduler == null)
                            break;
                        services.Remove(scheduler);
                    }

                    services.AddSingleton<IWorkflowScheduler, ImmediateWorkflowScheduler>();

                    services.AddAuthentication("Test")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                });
            });

            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");

            var assertionId = Guid.NewGuid();
            var httpAssertionConfig = JsonSerializer.Serialize(new
            {
                Id = assertionId,
                Field = "StatusCode",
                Operator = "Equal",
                HeaderKey = "",
                Expected = "201"
            });

            var httpTargetConfig = JsonSerializer.Serialize(new
            {
                Method = "Get",
                Url = server.Url("/"),
                Body = (object?)null,
                Headers = new Dictionary<string, string>(),
                Query = new Dictionary<string, string>(),
                Timeout = 5
            });

            var createResp = await client.PostAsJsonAsync("/api/monitor", new
            {
                Name = "IT HTTP FAIL",
                TargetType = "HTTP",
                TargetConfig = httpTargetConfig,
                IsEnabled = true,
                Assertions = new[]
                {
                    new
                    {
                        Id = assertionId,
                        Type = "HTTP",
                        ConfigJson = httpAssertionConfig
                    }
                }
            });

            createResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var monitorId = (await createResp.Content.ReadFromJsonAsync<Guid>())!;
            monitorId.Should().NotBe(Guid.Empty);

            var runResp = await client.PostAsync($"/api/monitor/{monitorId}/run",
                new StringContent("", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"));
            runResp.StatusCode.Should().Be(HttpStatusCode.OK);

            var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(8)));
            completed.Should().Be(received.Task);

            using var doc = await received.Task;
            var root = doc.RootElement;
            GetProperty(root, "type").GetString().Should().Be("monitor.execution.failed");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
            catch
            {
            }
        }
    }

    private static JsonElement GetProperty(JsonElement obj, string name)
    {
        foreach (var p in obj.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return p.Value;
        }

        throw new KeyNotFoundException($"Missing JSON property: {name}");
    }

    private static string GetWebApiContentRoot()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Path.Combine(root, "src", "AutoTest.Webapi");
    }

    private sealed class ImmediateWorkflowScheduler : IWorkflowScheduler
    {
        private readonly IServiceProvider _serviceProvider;

        public ImmediateWorkflowScheduler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task RunNowAsync(Guid workflowId)
        {
            using var scope = _serviceProvider.CreateScope();
            var monitorRepository = scope.ServiceProvider.GetRequiredService<IMonitorRepository>();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IOrchestrator>();
            var monitorService = scope.ServiceProvider.GetRequiredService<IMonitorService>();
            var monitor = await monitorRepository.GetByIdAsync(workflowId);
            if (monitor == null)
                return;

            var lockedBy = "test";
            var start = await monitorService.TryStartExecutionAsync(workflowId, idempotencyKey: null, lockedBy);
            if (!start.Started)
                return;

            monitor = await monitorRepository.GetByIdAsync(workflowId);
            if (monitor == null)
                return;

            await orchestrator.TryExecuteAsync(monitor, start.ExecutionId, start.StartedAtUtc, lockedBy);
        }

        public Task RunNowAsync(Guid workflowId, string? userId)
        {
            return RunNowAsync(workflowId);
        }

        public Task RunNowAsync(Guid workflowId, string? userId, string? idempotencyKey)
        {
            return RunNowAsync(workflowId);
        }

        public Task RunAfterAsync(Guid workflowId, TimeSpan delay)
        {
            return RunNowAsync(workflowId);
        }

        public Task ScheduleAsync(string jobId, string cron)
        {
            return Task.CompletedTask;
        }

        public Task UpsertDailyMonitorAsync(Guid monitorId, string timeHHmm)
        {
            return Task.CompletedTask;
        }

        public Task RemoveMonitorScheduleAsync(Guid monitorId)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class LocalHttpServer : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private LocalHttpServer(WebApplication app, Uri baseUri)
        {
            _app = app;
            BaseUri = baseUri;
        }

        public Uri BaseUri { get; }

        public string Url(string path)
        {
            var p = path.StartsWith("/") ? path : "/" + path;
            return new Uri(BaseUri, p).ToString();
        }

        public static async Task<LocalHttpServer> StartAsync(Func<HttpContext, Task> handler)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });

            builder.Services.AddRouting();
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(System.Net.IPAddress.Loopback, 0);
            });

            var app = builder.Build();
            app.MapFallback(handler);

            await app.StartAsync();

            var addresses = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses;
            var baseUri = new Uri(addresses.First());
            return new LocalHttpServer(app, baseUri);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            System.Text.Encodings.Web.UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim("sub", "test-user"),
                new Claim(ClaimTypes.NameIdentifier, "test-user"),
                new Claim(ClaimTypes.Role, "admin")
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

