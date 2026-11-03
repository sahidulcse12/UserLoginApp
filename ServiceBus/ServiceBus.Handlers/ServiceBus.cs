using Microsoft.Extensions.DependencyInjection;
using SharedSubsystem.DTOs.Abstractions;
using SharedSubsystem.Handlers.Abstructions;
using SharedSubsystem.Handlers.Events;

namespace ServiceBus.Handlers;

public class ServiceBus : IServiceBus
{
    private readonly IServiceProvider _serviceProvider;

    public ServiceBus(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<IEnumerable<Event>> SendCommandAsync<TCommand>(TCommand command) where TCommand : ICommand
    {
        var handler = _serviceProvider.GetService<ICommandHandler<TCommand>>();
        if (handler != null)
        {
            return await handler.HandleAsync(command);
        }
        return null!;
    }

    public async Task<TResult> SendQueryAsync<TQuery, TResult>(TQuery query) where TQuery : IQuery<TResult>
    {
        var handler = _serviceProvider.GetService<IQueryHandler<TQuery, TResult>>();
        if (handler == null)
        {
            throw new InvalidOperationException($"No query handler found for {typeof(TQuery).Name}");
        }

        return await handler.HandleAsync(query);
    }
}
