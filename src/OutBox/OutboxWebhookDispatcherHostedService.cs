using System.Text.Json;
using AutoTest.Core.Outbox;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace OutBox;

public sealed class OutboxWebhookDispatcherHostedService : BackgroundService
{
    public const string HttpClientName = "OutboxWebhook";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OutboxWebhookDispatcherHostedService> _logger;
    private readonly OutboxWebhookOptions _options;
    private readonly string _lockedBy;

    public OutboxWebhookDispatcherHostedService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IOptions<OutboxWebhookOptions> options,
        ILogger<OutboxWebhookDispatcherHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
        _lockedBy = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.Url))
        {
            _logger.LogInformation("Outbox webhook dispatcher disabled");
            return;
        }

        var pollInterval = TimeSpan.FromMilliseconds(Math.Max(200, _options.PollIntervalMs));
        var lockDuration = TimeSpan.FromSeconds(Math.Max(10, _options.LockSeconds));

        using var timer = new PeriodicTimer(pollInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchOnceAsync(lockDuration, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox webhook dispatcher error");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task DispatchOnceAsync(TimeSpan lockDuration, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<AutoTest.Core.Abstraction.IOutboxRepository>();

        var now = DateTime.UtcNow;
        var batch = await outboxRepository.LockNextBatchAsync(
            take: Math.Max(1, _options.BatchSize),
            lockDuration: lockDuration,
            lockedBy: _lockedBy,
            utcNow: now,
            cancellationToken: cancellationToken);

        if (batch.Count == 0)
            return;

        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds));

        foreach (var msg in batch)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await SendWebhookAsync(client, msg, cancellationToken);
                await outboxRepository.MarkSentAsync(msg.Id, _lockedBy, DateTime.UtcNow, cancellationToken);
            }
            catch (Exception ex)
            {
                var next = ComputeNextAttempt(msg.Attempts, DateTime.UtcNow);
                var error = ex.ToString();
                await outboxRepository.MarkFailedAsync(msg.Id, _lockedBy, error, next, cancellationToken);
                _logger.LogWarning(ex, "Outbox webhook send failed for {Id}", msg.Id);
            }
        }
    }

    private async Task SendWebhookAsync(HttpClient client, OutboxMessage message, CancellationToken cancellationToken)
    {
        JsonElement payloadElement;
        try
        {
            payloadElement = JsonSerializer.Deserialize<JsonElement>(message.PayloadJson);
        }
        catch
        {
            payloadElement = JsonSerializer.SerializeToElement(new { raw = message.PayloadJson });
        }

        var body = JsonSerializer.Serialize(new
        {
            id = message.Id,
            type = message.Type,
            occurredAt = message.OccurredAt,
            payload = payloadElement
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, _options.Url)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };

        using var resp = await client.SendAsync(req, cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            var text = await resp.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Webhook returned {(int)resp.StatusCode} {resp.ReasonPhrase}: {text}");
        }
    }

    private static DateTime ComputeNextAttempt(int attempts, DateTime utcNow)
    {
        var exp = Math.Min(10, Math.Max(0, attempts - 1));
        var delaySeconds = Math.Min(600, 5 * (int)Math.Pow(2, exp));
        return utcNow.AddSeconds(delaySeconds);
    }
}
