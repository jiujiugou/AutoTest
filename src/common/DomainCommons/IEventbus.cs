using System;

namespace EventCommons;

public interface IEventbus
{
    public Task PublishAsync(IEvent Event);
    public Task StopAsync();

}
