using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoTest.Application;
using AutoTest.Core;
using AutoTest.Core.Execution;
using AutoTest.Core.Abstraction;
using AutoTest.Core.Outbox;
using AutoTest.Application.ExecutionPipeline;

/// <summary>
/// 任务编排器：驱动一次监控任务从“执行”到“断言”再到“持久化/通知”的完整流程。
/// </summary>
/// <remarks>
/// 该编排器会在同一事务中落库：监控状态、执行记录、断言结果与 outbox 消息，
/// 从而保证“业务结果”和“对外通知”的一致性（Transactional Outbox）。
/// </remarks>
public class Orchestrator : IOrchestrator
{
    private readonly IPipeline _pipeline;
    private readonly IMonitorRepository _monitorRepository;
    private readonly IExecutionRecordRepository _executionRecordRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IUnitOfWork _unitOfWork;

    public Orchestrator(
        IPipeline pipeline,
        IMonitorRepository monitorRepository,
        IExecutionRecordRepository executionRecordRepository,
        IOutboxRepository outboxRepository,
        IUnitOfWork unitOfWork)
    {
        _pipeline = pipeline;
        _monitorRepository = monitorRepository;
        _executionRecordRepository = executionRecordRepository;
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// 尝试执行单个任务
    /// </summary>
    public async Task<ExecutionResult> TryExecuteAsync(MonitorEntity monitor, Guid executionId, DateTime startedAtUtc, string lockedBy)
    {
        var context = new PipelineContext(monitor);

        try
        {
            await _pipeline.ExecuteAsync(context);

            var result = context.Result ?? throw new InvalidOperationException("Pipeline did not produce a result");

            var isAssertionSuccess = result.Assertions.All(r => r.IsSuccess);
            if (result.IsExecutionSuccess && isAssertionSuccess)
                monitor.MarkSuccess();
            else
                monitor.MarkFailed();

            var finishedAt = DateTime.UtcNow;
            var resultJson = JsonSerializer.Serialize(result, result.GetType());

            var shouldNotify = !(result.IsExecutionSuccess && isAssertionSuccess);
            var outboxMessage = shouldNotify
                ? new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Type = "monitor.execution.failed",
                    PayloadJson = JsonSerializer.Serialize(new
                    {
                        MonitorId = monitor.Id,
                        MonitorName = monitor.Name,
                        TargetType = monitor.Target.Type,
                        ExecutionId = executionId,
                        StartedAt = startedAtUtc,
                        FinishedAt = finishedAt,
                        IsExecutionSuccess = result.IsExecutionSuccess,
                        IsAssertionSuccess = isAssertionSuccess,
                        ErrorMessage = result.ErrorMessage,
                        Assertions = result.Assertions
                    }),
                    OccurredAt = finishedAt,
                    Status = OutboxStatus.Pending,
                    Attempts = 0
                }
                : null;

            await _unitOfWork.ExecuteAsync(async tx =>
            {
                await _monitorRepository.UpdateAsync(monitor, tx);
                await _executionRecordRepository.UpdateCompletionAsync(
                    executionId,
                    (int)monitor.Status,
                    finishedAt,
                    result.IsExecutionSuccess,
                    result.ErrorMessage,
                    monitor.Target.Type,
                    resultJson,
                    tx);
                if (result.Assertions.Count > 0)
                    await _executionRecordRepository.AddAssertionResultsAsync(executionId, result.Assertions, tx);
                if (outboxMessage != null)
                    await _outboxRepository.AddAsync(outboxMessage, tx);
            });
            return result;
        }
        catch (Exception ex)
        {
            monitor.MarkFailed();

            var finishedAt = DateTime.UtcNow;
            var resultJson = JsonSerializer.Serialize(new { ex.Message, Exception = ex.ToString() });

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "monitor.execution.failed",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    MonitorId = monitor.Id,
                    MonitorName = monitor.Name,
                    TargetType = monitor.Target.Type,
                    ExecutionId = executionId,
                    StartedAt = startedAtUtc,
                    FinishedAt = finishedAt,
                    IsExecutionSuccess = false,
                    IsAssertionSuccess = false,
                    ErrorMessage = ex.Message,
                    Exception = ex.ToString()
                }),
                OccurredAt = finishedAt,
                Status = OutboxStatus.Pending,
                Attempts = 0
            };

            await _unitOfWork.ExecuteAsync(async tx =>
            {
                await _monitorRepository.UpdateAsync(monitor, tx);
                await _executionRecordRepository.UpdateCompletionAsync(
                    executionId,
                    (int)monitor.Status,
                    finishedAt,
                    false,
                    ex.Message,
                    "Exception",
                    resultJson,
                    tx);
                await _outboxRepository.AddAsync(outboxMessage, tx);
            });

            throw;
        }
    }

}
