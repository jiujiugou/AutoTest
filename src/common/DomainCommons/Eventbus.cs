using System;
using System.Collections;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
namespace EventCommons;

public class Eventbus : IEventbus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Channel<IEvent> _eventChannel;
    private readonly CancellationTokenSource _cancellationTokenSource;
    public Eventbus(IServiceProvider serviceProvider, int capacity = 100)
    {
        _serviceProvider = serviceProvider;
        _eventChannel = Channel.CreateBounded<IEvent>(capacity);
        _cancellationTokenSource = new CancellationTokenSource();
        _ = ProcessQueAsync();
    }

    public async Task PublishAsync(IEvent Event)
    {
        await _eventChannel.Writer.WriteAsync(Event);
    }
    private async Task ProcessQueAsync()
    {
        await foreach (var domainEvent in _eventChannel.Reader.ReadAllAsync(_cancellationTokenSource.Token))
        {
            try
            {
                var handlerType = typeof(IEventHandler<>).MakeGenericType(domainEvent.GetType());
                var handlers = _serviceProvider.GetServices(handlerType);
                if (handlers is not null)
                {
                    foreach (var handler in (IEnumerable)handlers)
                    {
                        await ((dynamic)handler).Handle((dynamic)domainEvent);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing event: {ex.Message}");
            }
        }
    }
    public async Task StopAsync()
    {
        // 标记不再接收新事件
        _eventChannel.Writer.Complete();

        try
        {
            // 等待队列里已有事件处理完
            await _eventChannel.Reader.Completion;
        }
        catch (OperationCanceledException)
        {
            // 忽略取消异常
        }

        // 最后取消 Token，确保后台任务退出
        _cancellationTokenSource.Cancel();
    }
}
