using System;

namespace EventCommons;

public interface IEventHandler<TEvent> where TEvent : IEvent
{
    Task HandleAsync(TEvent domainEvent);
}
