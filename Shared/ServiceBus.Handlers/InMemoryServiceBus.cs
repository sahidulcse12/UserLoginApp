using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedSubsystem.DTOs.Abstractions;
using SharedSubsystem.Handlers.Abstractions;

namespace ServiceBus.Handlers;

public class InMemoryServiceBus : IServiceBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryServiceBus> _logger;

    public InMemoryServiceBus(IServiceProvider serviceProvider, ILogger<InMemoryServiceBus> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<TResult> SendCommandAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        var commandType = command.GetType();
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(commandType, typeof(TResult));

        var handler = _serviceProvider.GetService(handlerType);
        if (handler is null)
        {
            throw new InvalidOperationException($"No command handler registered for command '{commandType.Name}'.");
        }

        _logger.LogInformation("[ServiceBus] Routing command '{CommandType}' to handler '{HandlerType}'", 
            commandType.Name, handler.GetType().Name);

        var method = handlerType.GetMethod(nameof(ICommandHandler<ICommand<TResult>, TResult>.HandleAsync));
        if (method is null)
        {
            throw new InvalidOperationException($"Handler '{handler.GetType().Name}' does not implement HandleAsync.");
        }

        var task = (Task<TResult>)method.Invoke(handler, new object[] { command, cancellationToken })!;
        return await task;
    }

    public async Task BroadcastEventAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IEvent
    {
        var eventType = @event.GetType();
        var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);

        var handlers = _serviceProvider.GetServices(handlerType);

        _logger.LogInformation("[ServiceBus] Broadcasting event '{EventType}' (Occurred: {OccurredOn})", 
            eventType.Name, @event.OccurredOn);

        foreach (var handler in handlers)
        {
            if (handler is null) continue;

            var method = handlerType.GetMethod(nameof(IEventHandler<TEvent>.HandleAsync));
            if (method is not null)
            {
                var task = (Task)method.Invoke(handler, new object[] { @event, cancellationToken })!;
                await task;
            }
        }
    }
}
