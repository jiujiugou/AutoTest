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

            var body = BuildMessage(notification);

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

    private static string BuildMessage(MonitorExecutionFailedEvent notification)
    {
        try
        {
            var p = JsonSerializer.Deserialize<MonitorExecutionFailedPayload>(notification.Payload);
            if (p == null)
                return FallbackMessage(notification.ExecutionId);

            var name = EscapeMarkdown(p.MonitorName);
            var type = p.TargetType ?? "?";
            var err = Truncate(EscapeMarkdown(p.ErrorMessage ?? ""), 200);
            var failureLabel = p.FailureType switch
            {
                FailureType.Execution => "<font color='#ff4d4f'>执行失败</font>",
                FailureType.Assertion => "<font color='#faad14'>断言失败</font>",
                FailureType.Exception => "<font color='#ff4d4f'>异常</font>",
                _ => "失败"
            };
            var time = p.FinishedAt == default ? DateTime.UtcNow : p.FinishedAt;
            var duration = (time - p.StartedAt).TotalSeconds;

            var failedLines = new System.Text.StringBuilder();
            if (p.Assertions?.Count > 0)
            {
                foreach (var a in p.Assertions.Where(x => !x.IsSuccess))
                {
                    var target = EscapeMarkdown(a.Target);
                    var detail = Truncate(EscapeMarkdown(a.Message ?? a.Actual ?? ""), 80);
                    failedLines.AppendLine($"> {target}: {detail}");
                }
            }

            var text = $"""
                ## 🔴 {name}

                **类型**: {type}　|　{failureLabel}　|　耗时 {duration:F1}s

                **错误**: {err}

                {(failedLines.Length > 0 ? $"**断言详情**\n{failedLines}" : "")}
                **时间**: {time:yyyy-MM-dd HH:mm:ss} UTC

                [查看详情](https://autotest.example.com)　ID: `{notification.ExecutionId}`
                """;

            return JsonSerializer.Serialize(new
            {
                msgtype = "markdown",
                markdown = new
                {
                    title = $"AutoTest: {p.MonitorName}",
                    text
                }
            });
        }
        catch
        {
            return FallbackMessage(notification.ExecutionId);
        }
    }

    private static string FallbackMessage(Guid executionId)
    {
        return JsonSerializer.Serialize(new
        {
            msgtype = "text",
            text = new
            {
                content = $"AutoTest 监控执行失败\nID: {executionId}"
            }
        });
    }

    private static string EscapeMarkdown(string s)
    {
        return s.Replace("\\", "\\\\")
                .Replace("*", "\\*")
                .Replace("_", "\\_")
                .Replace("#", "\\#")
                .Replace("`", "\\`")
                .Replace(">", "\\>")
                .Replace("<", "&lt;")
                .Replace("\r", "")
                .Replace("\n", " ");
    }

    private static string Truncate(string s, int maxLen)
    {
        return s.Length <= maxLen ? s : s[..maxLen] + "...";
    }
}