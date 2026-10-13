using SharedSubsystem.DTOs.Abstractions;

namespace SharedSubsystem.DTOs.IntegrationEvents;

public record UserLoggedOutEvent(
    string Username,
    DateTime OccurredOn
) : IEvent;
