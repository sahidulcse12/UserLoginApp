using SharedSubsystem.DTOs.Abstractions;

namespace SharedSubsystem.Handlers.Abstractions;

public interface IEventHandler<in TEvent> where TEvent : IEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
