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
