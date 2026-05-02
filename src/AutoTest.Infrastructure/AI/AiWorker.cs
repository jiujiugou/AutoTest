using AutoTest.AI;
using AutoTest.Application;
using AutoTest.Core.AI;
using AutoTest.Core.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AutoTest.Infrastructure.AI
{
    public class AiWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AiWorker> _logger;
        private readonly AiWorkerOptions _opts;

        public AiWorker(
            IServiceScopeFactory scopeFactory,
            IOptions<AiWorkerOptions> options,
            ILogger<AiWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _opts = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var taskService = scope.ServiceProvider.GetRequiredService<IAiTaskService>();
                    var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
                    var analysisRepository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
                    var aiClient = scope.ServiceProvider.GetRequiredService<IAiClient>();

                    var tasks = await taskService.TakeBatchAsync(_opts.BatchSize, ct);

                    if (tasks.Count == 0)
                    {
                        await Task.Delay(_opts.PollIntervalMs, ct);
                        continue;
                    }

                    _logger.LogInformation("AI Worker fetched {Count} tasks", tasks.Count);

                    using var semaphore = new SemaphoreSlim(_opts.Parallelism);

                    var running = tasks.Select(async task =>
                    {
                        await semaphore.WaitAsync(ct);
                        try
                        {
                            await ProcessOne(
                                task,
                                taskService,
                                logService,
                                analysisRepository,
                                aiClient,
                                ct);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    await Task.WhenAll(running);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AI worker loop failed");
                    await Task.Delay(_opts.ErrorDelayMs, ct);
                }
            }
        }

        private async Task ProcessOne(
    AiTask task,
    IAiTaskService taskService,
    ILogService logService,
    IAnalysisRepository analysisRepository,
    IAiClient aiClient,
    CancellationToken ct)
        {
            try
            {
                var input = JsonSerializer.Deserialize<AiAnalysisInputDto>(task.InputJson);

                var traceData = await BuildTraceData(input, logService);

                var systemPrompt = AiAnalysisPromptBuilder.BuildSystemPrompt();
                var userPrompt = AiAnalysisPromptBuilder.BuildUserPrompt(
                    input ?? new AiAnalysisInputDto(),
                    traceData);

                var fullPrompt = $"{systemPrompt}\n\n{userPrompt}";

                // ✅ 防 token 爆炸
                if (fullPrompt.Length > 6000)
                {
                    _logger.LogWarning("Prompt truncated. Task={TaskId}", task.Id);
                    fullPrompt = fullPrompt[..6000];
                }

                string? resultJson = null;

                // ✅ 重试（指数退避）
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        resultJson = await aiClient.AnalyzeAsync(fullPrompt, ct);

                        // ✅ 校验 JSON
                        if (IsValidJson(resultJson))
                            break;

                        throw new Exception("Invalid JSON returned");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Retry {Retry} failed", i + 1);

                        await Task.Delay((int)Math.Pow(2, i) * 1000, ct);
                    }
                }

                // ❌ 彻底失败 fallback
                if (resultJson == null)
                {
                    resultJson = BuildFallbackJson();
                }

                var output = SafeDeserialize(resultJson);

                var analysis = new AIAnalysis
                {
                    Id = Guid.NewGuid(),
                    ExecutionRecordId = Guid.TryParse(input?.TraceId, out var eid) ? eid : Guid.Empty,
                    OutboxMessageId = task.BizId,
                    Type = output?.Type ?? "Unknown",
                    Severity = output?.Severity ?? "low",
                    Category = output?.Category ?? "",
                    RootCause = output?.RootCause ?? "AI failed",
                    Suggestion = output?.Suggestion ?? "Retry or check logs",
                    Summary = output?.Summary ?? "",
                    Confidence = output?.Confidence ?? 0,
                    InputJson = task.InputJson ?? "",
                    OutputJson = resultJson,
                    Model = "doubao-1.5-lite-32k",
                    PromptVersion = AiAnalysisPromptBuilder.PromptVersion,
                    CreatedAt = DateTime.UtcNow,
                    ProcessedAt = DateTime.UtcNow
                };

                await analysisRepository.AddAsync(analysis);
                await taskService.MarkCompletedAsync(task.Id, resultJson, ct);
            }
            catch (Exception ex)
            {
                var next = ComputeNext(task.Attempts);

                await taskService.MarkFailedAsync(task.Id, ex.ToString(), next, ct);

                _logger.LogWarning(ex, "AI task failed {Id}", task.Id);
            }
        }

        private static DateTime ComputeNext(int attempts)
        {
            var exp = Math.Min(6, Math.Max(0, attempts));
            var seconds = Math.Min(300, 5 * (int)Math.Pow(2, exp));
            return DateTime.UtcNow.AddSeconds(seconds);
        }
        private async Task<TraceContextData> BuildTraceData(
    AiAnalysisInputDto? input,
    ILogService logService)
        {
            if (string.IsNullOrEmpty(input?.TraceId))
                return new TraceContextData();

            var logs = await logService.GetAiErrorContextAsync(
                input.TraceId,
                input.DateTime,
                _opts.EsWindowSeconds,
                _opts.EsTake);

            logs = logs
                .OrderByDescending(x => x.Timestamp)
                .Take(20)
                .OrderBy(x => x.Timestamp)
                .ToList();

            var keyEvents = logs
                .Where(x =>
                    x.Level == "ERROR" ||
                    x.Level == "FATAL" ||
                    x.Message.Contains("fail") ||
                    x.Message.Contains("exception") ||
                    x.Message.Contains("timeout"))
                .ToList();

            return new TraceContextData
            {
                Logs = logs,
                KeyEvents = keyEvents,
                TraceId = input.TraceId,
                StartTime = logs.FirstOrDefault()?.Timestamp,
                EndTime = logs.LastOrDefault()?.Timestamp,
                WindowSeconds = _opts.EsWindowSeconds
            };
        }
        private static bool IsValidJson(string str)
        {
            try
            {
                JsonDocument.Parse(str);
                return true;
            }
            catch
            {
                return false;
            }
        }
        private static AiAnalysisOutputDto? SafeDeserialize(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<AiAnalysisOutputDto>(json);
            }
            catch
            {
                return null;
            }
        }
        private static string BuildFallbackJson()
        {
            return """
{
  "type": "Unknown",
  "severity": "low",
  "category": "AIError",
  "summary": "AI analysis failed",
  "rootCause": "模型调用失败或返回非法数据",
  "suggestion": "请检查日志或重试",
  "impact": "single_request",
  "faultService": "",
  "confidence": 0.1,
  "errorChain": []
}
""";
        }
    }
    public class TraceContextData
    {
        public List<TraceLogEntry> Logs { get; set; } = new();

        // 新增：关键事件（比 Logs 更重要）
        public List<TraceLogEntry> KeyEvents { get; set; } = new();

        public int WindowSeconds { get; set; } = 30;

        public DateTime? StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public string? TraceId { get; set; }
    }
}