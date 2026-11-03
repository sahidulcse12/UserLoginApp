using SharedSubsystem.DTOs.Abstractions;
using SharedSubsystem.Handlers.Events;

namespace SharedSubsystem.Handlers.Abstractions;

public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

public interface IEventHandler<in TEvent> where TEvent : IEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}

public interface IServiceBus
{
    Task<IEnumerable<Event>> SendCommandAsync<TCommand>(TCommand command) where TCommand : ICommand;
    Task<TResult> SendQueryAsync<TQuery, TResult>(TQuery query) where TQuery : IQuery<TResult>;
    Task<TResult> SendCommandAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);
    Task BroadcastEventAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IEvent;
}
