using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoTest.Application;
using AutoTest.Core;
using AutoTest.Core.Execution;
using AutoTest.Core.Abstraction;
using AutoTest.Application.ExecutionPipeline;

public class Orchestrator : IOrchestrator
{
    private readonly IPipeline _pipeline;
    private readonly IMonitorRepository _monitorRepository;
    private readonly IExecutionRecordRepository _executionRecordRepository;
    private readonly IUnitOfWork _unitOfWork;

    public Orchestrator(
        IPipeline pipeline,
        IMonitorRepository monitorRepository,
        IExecutionRecordRepository executionRecordRepository,
        IUnitOfWork unitOfWork)
    {
        _pipeline = pipeline;
        _monitorRepository = monitorRepository;
        _executionRecordRepository = executionRecordRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// 尝试执行单个任务
    /// </summary>
    public async Task<ExecutionResult> TryExecuteAsync(MonitorEntity monitor)
    {
        if (!monitor.CanExecute())
            throw new InvalidOperationException("Monitor cannot execute");

        var startedAt = DateTime.UtcNow;
        monitor.MarkRunning();

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

            var executionId = Guid.NewGuid();
            var record = new ExecutionRecord(
                executionId,
                monitor.Id,
                monitor.Status,
                startedAt,
                finishedAt,
                result.IsExecutionSuccess,
                result.ErrorMessage,
                monitor.Target.Type,
                JsonSerializer.Serialize(result, result.GetType())
            );

            await _unitOfWork.ExecuteAsync(async tx =>
            {
                await _monitorRepository.UpdateAsync(monitor, tx);
                await _executionRecordRepository.AddAsync(record, tx);
                if (result.Assertions.Count > 0)
                    await _executionRecordRepository.AddAssertionResultsAsync(executionId, result.Assertions, tx);
            });
            return result;
        }
        catch (Exception ex)
        {
            monitor.MarkFailed();

            var finishedAt = DateTime.UtcNow;
            var executionId = Guid.NewGuid();
            var record = new ExecutionRecord(
                executionId,
                monitor.Id,
                monitor.Status,
                startedAt,
                finishedAt,
                false,
                ex.Message,
                "Exception",
                JsonSerializer.Serialize(new { ex.Message, Exception = ex.ToString() })
            );

            await _unitOfWork.ExecuteAsync(async tx =>
            {
                await _monitorRepository.UpdateAsync(monitor, tx);
                await _executionRecordRepository.AddAsync(record, tx);
            });

            throw;
        }
    }

}
