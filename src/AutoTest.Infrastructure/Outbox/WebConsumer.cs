using AutoTest.Core.Outbox;
using AutoTest.Infrastructure.Outbox;
using EventCommons;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

internal class WebhookConsumer : INotificationHandler<MonitorExecutionFailedEvent>
{
    public const string HttpClientName = "OutboxWebhook";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookConsumer> _logger;
    private readonly WebhookOptions _options;

    public WebhookConsumer(
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookConsumer> logger,
        IOptions<WebhookOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
    }

    public async Task Handle(MonitorExecutionFailedEvent notification, CancellationToken ct)
    {
        if (!_options.Enabled) return;

        if (string.IsNullOrWhiteSpace(_options.Url))
            return;

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

            var body = JsonSerializer.Serialize(new
            {
                msgtype = "text",
                text = new
                {
                    content = $"AutoTest:执行失败\nID: {notification.ExecutionId}"
                }
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, _options.Url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            using var resp = await client.SendAsync(req, ct);

            if (!resp.IsSuccessStatusCode)
            {
                var text = await resp.Content.ReadAsStringAsync(ct);
                throw new Exception($"Webhook failed: {text}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook error");
            throw;
        }
    }
}