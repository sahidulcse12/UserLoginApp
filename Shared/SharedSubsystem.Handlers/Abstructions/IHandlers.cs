using SharedSubsystem.DTOs.Abstractions;
using SharedSubsystem.Handlers.Events;

namespace SharedSubsystem.Handlers.Abstructions;

public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task<IEnumerable<Event>> HandleAsync(TCommand command);
}

public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query);
}

public interface IServiceBus
{
    Task<IEnumerable<Event>> SendCommandAsync<TCommand>(TCommand command) where TCommand : ICommand;
    Task<TResult> SendQueryAsync<TQuery, TResult>(TQuery query) where TQuery : IQuery<TResult>;
}
