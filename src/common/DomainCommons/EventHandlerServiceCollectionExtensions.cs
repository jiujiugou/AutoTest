using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace EventCommons;

public static class EventHandlerServiceCollectionExtensions
{
    //扫描程序集，把所有实现了 IEventHandler<> 的具体类都找出来，方便注册或动态调用。

    public static IServiceCollection AddEventHandlers(this IServiceCollection services, params Assembly[] assemblies)
    {
        // 默认扫描当前程序集
        if (assemblies == null || assemblies.Length == 0)
            assemblies = new[] { Assembly.GetCallingAssembly() };

        var handlerList = new List<(Type, Type)>();

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .SelectMany(t => t.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                    .Select(i => (InterfaceType: i, HandlerType: t)));
            foreach (var handler in types)
            {
                services.AddScoped(handler.InterfaceType, handler.HandlerType);
            }
        }

        return services;
    }
}
