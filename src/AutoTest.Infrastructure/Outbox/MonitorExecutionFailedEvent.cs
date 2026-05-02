using AutoTest.Core.Outbox;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace EventCommons
{
    public record MonitorExecutionFailedEvent(
    Guid ExecutionId,
    Guid OutboxMessageId,
    string Payload
) : INotification;
}
