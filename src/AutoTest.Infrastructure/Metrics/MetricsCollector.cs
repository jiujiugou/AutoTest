using System.Threading;

namespace AutoTest.Infrastructure.Metrics;

/// <summary>
/// 关键运行时指标收集（线程安全）。
/// </summary>
public class MetricsCollector
{
    // ── 执行指标 ──
    private long _executionTotal;
    private long _executionSuccess;
    private long _executionFail;
    private long _executionTotalMs;

    // ── AI 分析指标 ──
    private long _aiAnalysisTotal;
    private long _aiAnalysisFail;
    private long _aiAnalysisTotalMs;

    // ── 执行 ──
    public void RecordExecution(bool success, long elapsedMs)
    {
        Interlocked.Increment(ref _executionTotal);
        if (success)
            Interlocked.Increment(ref _executionSuccess);
        else
            Interlocked.Increment(ref _executionFail);
        Interlocked.Add(ref _executionTotalMs, elapsedMs);
    }

    // ── AI 分析 ──
    public void RecordAiAnalysis(long elapsedMs)
    {
        Interlocked.Increment(ref _aiAnalysisTotal);
        Interlocked.Add(ref _aiAnalysisTotalMs, elapsedMs);
    }

    public void RecordAiAnalysisFailure()
    {
        Interlocked.Increment(ref _aiAnalysisFail);
    }

    // ── 快照 ──
    public MetricsSnapshot Snapshot()
    {
        var execTotal = Interlocked.Read(ref _executionTotal);
        var execSuccess = Interlocked.Read(ref _executionSuccess);
        var execFail = Interlocked.Read(ref _executionFail);
        var execMs = Interlocked.Read(ref _executionTotalMs);

        var aiTotal = Interlocked.Read(ref _aiAnalysisTotal);
        var aiFail = Interlocked.Read(ref _aiAnalysisFail);
        var aiMs = Interlocked.Read(ref _aiAnalysisTotalMs);

        return new MetricsSnapshot
        {
            ExecutionTotal = execTotal,
            ExecutionSuccess = execSuccess,
            ExecutionFail = execFail,
            ExecutionSuccessRate = execTotal > 0 ? (double)execSuccess / execTotal : 0,
            ExecutionAvgMs = execTotal > 0 ? execMs / (double)execTotal : 0,

            AiAnalysisTotal = aiTotal,
            AiAnalysisFail = aiFail,
            AiAnalysisAvgMs = aiTotal > 0 ? aiMs / (double)aiTotal : 0,

            Timestamp = DateTime.UtcNow
        };
    }
}

public class MetricsSnapshot
{
    public long ExecutionTotal { get; init; }
    public long ExecutionSuccess { get; init; }
    public long ExecutionFail { get; init; }
    public double ExecutionSuccessRate { get; init; }
    public double ExecutionAvgMs { get; init; }

    public long AiAnalysisTotal { get; init; }
    public long AiAnalysisFail { get; init; }
    public double AiAnalysisAvgMs { get; init; }

    public DateTime Timestamp { get; init; }
}
