using System.Net;
using System.Net.Http.Json;
using AutoTest.Webapi.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace AutoTest.Tests.Webapi;

public class AiControllerTests
{
    [Fact]
    public async Task Chat_ShouldReturnHint_WhenAiNotConfigured()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"autotest-ai-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var dbPath = Path.Combine(tempRoot, "AutoTestDb.sqlite");
        var hangfireDbPath = Path.Combine(tempRoot, "hangfire.db");
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(GetWebApiContentRoot());
            builder.UseSetting("Database:Provider", "Sqlite");
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={dbPath};");
            builder.UseSetting("ConnectionStrings:HangfireConnection", $"Data Source={hangfireDbPath};Foreign Keys=True;");
            builder.UseSetting("AI:ApiKey", "YOUR_DOUBAO_API_KEY");
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "Sqlite",
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={dbPath};",
                    ["ConnectionStrings:HangfireConnection"] = $"Data Source={hangfireDbPath};Foreign Keys=True;",
                    ["AI:ApiKey"] = "YOUR_DOUBAO_API_KEY"
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
            });
        });

        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/AiAgent/chat", new { message = "ping" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        body.Should().NotBeNull();
        body!.Should().ContainKey("text");
        body["text"].Should().Contain("提示");

        try
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
        catch
        {
        }
    }

    [Fact]
    public async Task Chat_ShouldReturnBadRequest_WhenMessageMissing()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"autotest-ai-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var dbPath = Path.Combine(tempRoot, "AutoTestDb.sqlite");
        var hangfireDbPath = Path.Combine(tempRoot, "hangfire.db");
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
            });
        });

        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/AiAgent/chat", new { message = "" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        try
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
        catch
        {
        }
    }

    private static string GetWebApiContentRoot()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Path.Combine(root, "src", "AutoTest.Webapi");
    }
}
