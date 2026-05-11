using AutoTest.Core.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoTest.Infrastructure.AI;

/// <summary>
/// AI 调用熔断器：连续失败 N 次后断开，冷却后自动恢复。
/// </summary>
public class AiCircuitBreaker
{
    private readonly int _threshold;
    private readonly int _cooldownMs;
    private readonly ILogger<AiCircuitBreaker> _logger;

    private int _consecutiveFailures;
    private DateTime? _openedAt;
    private bool _isOpen;

    public AiCircuitBreaker(IOptions<AiWorkerOptions> options, ILogger<AiCircuitBreaker> logger)
    {
        _threshold = options.Value.CircuitBreakerThreshold;
        _cooldownMs = options.Value.CircuitBreakerCooldownMs;
        _logger = logger;
    }

    public bool IsOpen
    {
        get
        {
            if (_threshold <= 0) return false; // 熔断禁用

            if (!_isOpen) return false;

            // 冷却期过了 → 恢复
            if (_openedAt != null && (DateTime.UtcNow - _openedAt.Value).TotalMilliseconds > _cooldownMs)
            {
                _isOpen = false;
                _consecutiveFailures = 0;
                _logger.LogInformation("AI 熔断冷却完成，恢复调用");
                return false;
            }

            return true;
        }
    }

    public void RecordSuccess()
    {
        if (_threshold <= 0) return;
        _consecutiveFailures = 0;
        _isOpen = false;
        _openedAt = null;
    }

    public void RecordFailure()
    {
        if (_threshold <= 0) return;

        Interlocked.Increment(ref _consecutiveFailures);

        if (_consecutiveFailures >= _threshold && !_isOpen)
        {
            _isOpen = true;
            _openedAt = DateTime.UtcNow;
            _logger.LogWarning("AI 熔断已断开！连续失败 {Count} 次，冷却 {Cooldown}s",
                _consecutiveFailures, _cooldownMs / 1000);
        }
    }
}
