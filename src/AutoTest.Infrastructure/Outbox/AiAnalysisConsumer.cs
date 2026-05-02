using AutoTest.Application;
using AutoTest.Core.AI;
using AutoTest.Core.Outbox;
using EventCommons;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace AutoTest.Infrastructure.Outbox
{

    internal class AiAnalysisConsumer : INotificationHandler<MonitorExecutionFailedEvent>
    {
        private readonly IAiTaskService _aiTaskService;
        private readonly ILogger<AiAnalysisConsumer> _logger;

        public AiAnalysisConsumer(IAiTaskService aiTaskService, ILogger<AiAnalysisConsumer> logger)
        {
            _aiTaskService = aiTaskService;
            _logger = logger;
        }

        public async Task Handle(MonitorExecutionFailedEvent notification, CancellationToken ct)
        {
            // 反序列化 Payload
            var payload = JsonSerializer.Deserialize<MonitorExecutionFailedPayload>(notification.Payload);
            var input = new AiAnalysisInputDto
            {
                ExceptionType = payload?.Exception?.Type,
                ErrorMessage = payload?.ErrorMessage,
                DateTime = DateTime.UtcNow,
                StackTrace = payload?.Exception?.StackTrace != null && payload.Exception.StackTrace.Length > 2048
                    ? payload.Exception.StackTrace.Substring(0, 2048) + "..." : payload?.Exception?.StackTrace,
                TraceId = payload?.ExecutionId.ToString(),
                FailedAssertions = payload?.Assertions?.FindAll(a => !a.IsSuccess)?.ConvertAll(a => new AssertionSummary
                {
                    Target = a.Target,
                    Message = a.Message
                })
            };
            try
            {
                _logger.LogInformation("Enqueuing AI analysis task for OutboxMessageId: {OutboxMessageId}", notification.OutboxMessageId);
                await _aiTaskService.EnqueueAsync(new AiTask
                {
                    TaskType = "MonitorExecutionFailed",
                    BizId = notification.OutboxMessageId,
                    InputJson = JsonSerializer.Serialize(input),
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                }, ct);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use your preferred logging framework)
                _logger.LogError(ex, "Failed to enqueue AI analysis task");
            }
        }
    }
}
