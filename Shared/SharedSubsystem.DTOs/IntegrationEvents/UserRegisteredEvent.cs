using SharedSubsystem.DTOs.Abstractions;

namespace SharedSubsystem.DTOs.IntegrationEvents;

public record UserRegisteredEvent(
    Guid UserId,
    string Username,
    string Email,
    string UserType,
    string? KeycloakId,
    DateTime OccurredOn
) : IEvent;
