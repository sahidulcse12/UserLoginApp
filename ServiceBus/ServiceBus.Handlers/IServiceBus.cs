using SharedSubsystem.DTOs.Abstractions;
using SharedSubsystem.Handlers.Events;

namespace ServiceBus.Handlers;

public interface IServiceBus
{
    Task<IEnumerable<Event>> SendCommandAsync<TCommand>(TCommand command) where TCommand : ICommand;
    Task<TResult> SendQueryAsync<TQuery, TResult>(TQuery query) where TQuery : IQuery<TResult>;
}
