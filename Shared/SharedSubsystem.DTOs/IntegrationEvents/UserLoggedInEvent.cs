using SharedSubsystem.DTOs.Abstractions;

namespace SharedSubsystem.DTOs.IntegrationEvents;

public record UserLoggedInEvent(
    string Username,
    DateTime OccurredOn
) : IEvent;
