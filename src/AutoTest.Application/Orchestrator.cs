using AutoTest.Application;
using AutoTest.Core;
using AutoTest.Core.Abstraction;
using AutoTest.Core.Execution;
using AutoTest.Core.ExecutionPipeline;
using AutoTest.Core.Outbox;
using System.Text.Json;

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

    public async Task<ExecutionResult> TryExecuteAsync(
        MonitorEntity monitor,
        Guid executionId,
        DateTime startedAtUtc,
        string lockedBy)
    {
        var context = new PipelineContext(monitor);

        try
        {
            await _pipeline.ExecuteAsync(context);

            var result = context.Result ?? throw new InvalidOperationException("No result");

            var isAssertionSuccess = result.Assertions.All(r => r.IsSuccess);
            var success = result.IsExecutionSuccess && isAssertionSuccess;

            // ✅ 统一状态设置
            if (success)
                monitor.MarkSuccess();
            else
                monitor.MarkFailed();

            var finishedAt = DateTime.UtcNow;

            if (success)
            {
                await _unitOfWork.ExecuteAsync(async tx =>
                {
                    await _monitorRepository.UpdateAsync(monitor, tx);

                    await _executionRecordRepository.UpdateCompletionAsync(
                        executionId,
                        (int)monitor.Status,
                        finishedAt,
                        true,
                        null,
                        monitor.Target.Type,
                        JsonSerializer.Serialize(result),
                        tx);

                    if (result.Assertions.Count > 0)
                        await _executionRecordRepository.AddAssertionResultsAsync(
                            executionId, result.Assertions, tx);
                });

                return result;
            }

            var payload = BuildFailurePayload(
                monitor,
                executionId,
                startedAtUtc,
                finishedAt,
                result,
                isAssertionSuccess,
                null // 无异常
            );

            var outbox = BuildOutbox(payload, finishedAt);

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
                    JsonSerializer.Serialize(result),
                    tx);

                if (result.Assertions.Count > 0)
                    await _executionRecordRepository.AddAssertionResultsAsync(
                        executionId, result.Assertions, tx);

                await _outboxRepository.AddAsync(outbox, tx);
            });

            return result;
        }
        catch (Exception ex)
        {
            monitor.MarkFailed();

            var finishedAt = DateTime.UtcNow;

            var payload = BuildFailurePayload(
                monitor,
                executionId,
                startedAtUtc,
                finishedAt,
                null,
                false,
                ex // ✅ 异常统一走这里
            );

            var outbox = BuildOutbox(payload, finishedAt);

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
                    JsonSerializer.Serialize(payload),
                    tx);

                await _outboxRepository.AddAsync(outbox, tx);
            });

            throw;
        }
    }

    private static MonitorExecutionFailedPayload BuildFailurePayload(
        MonitorEntity monitor,
        Guid executionId,
        DateTime startedAt,
        DateTime finishedAt,
        ExecutionResult? result,
        bool isAssertionSuccess,
        Exception? ex)
    {
        // 判定失败类型
        var failureType = ex != null
            ? FailureType.Exception
            : !result!.IsExecutionSuccess
                ? FailureType.Execution
                : FailureType.Assertion;

        return new MonitorExecutionFailedPayload
        {
            MonitorId = monitor.Id,
            MonitorName = monitor.Name,
            ExecutionId = executionId,

            StartedAt = startedAt,
            FinishedAt = finishedAt,

            FailureType = failureType,

            IsExecutionSuccess = result?.IsExecutionSuccess ?? false,
            IsAssertionSuccess = isAssertionSuccess,

            ErrorMessage = ex?.Message ?? result?.ErrorMessage,

            Assertions = result?.Assertions,

            Exception = ex == null ? null : new ExceptionInfo
            {
                Type = ex.GetType().Name,
                Message = ex.Message,
                StackTrace = ex.StackTrace
            }
        };
    }

    private static OutboxMessage BuildOutbox(
        MonitorExecutionFailedPayload payload,
        DateTime occurredAt)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "monitor.execution.failed",
            PayloadJson = JsonSerializer.Serialize(payload),
            OccurredAt = occurredAt,
            Status = OutboxStatus.Pending,
            Attempts = 0
        };
    }
}