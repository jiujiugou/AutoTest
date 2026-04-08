using System.Net;
using System.Net.Http.Json;
using AutoTest.Webapi.Ai;
using AutoTest.Webapi.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace AutoTest.Tests.Webapi;

public class AiControllerTests
{
    [Fact]
    public async Task Chat_ShouldReturnReply_FromService()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(GetWebApiContentRoot());
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAiChatService));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddSingleton<IAiChatService>(new FakeAiChatService("hello"));
            });
        });

        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/ai/chat", new { message = "ping" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<AiChatResponse>();
        body.Should().NotBeNull();
        body!.Reply.Should().Be("hello");
    }

    [Fact]
    public async Task Chat_ShouldReturnBadRequest_WhenMessageMissing()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(GetWebApiContentRoot());
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAiChatService));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddSingleton<IAiChatService>(new FakeAiChatService("ignored"));
            });
        });

        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/ai/chat", new { message = "" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static string GetWebApiContentRoot()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Path.Combine(root, "src", "AutoTest.Webapi");
    }

    private sealed class FakeAiChatService : IAiChatService
    {
        private readonly string _reply;

        public FakeAiChatService(string reply)
        {
            _reply = reply;
        }

        public Task<AiChatResult> ChatAsync(string userMessage, string? systemMessage, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AiChatResult(_reply));
        }
    }
}
