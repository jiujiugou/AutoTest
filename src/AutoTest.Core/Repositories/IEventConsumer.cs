using System;
using System.Collections.Generic;
using System.Text;

namespace EventCommons
{
    public interface IEventConsumer<in T>
    {
        Task HandleAsync(T message, CancellationToken ct);
    }
}
