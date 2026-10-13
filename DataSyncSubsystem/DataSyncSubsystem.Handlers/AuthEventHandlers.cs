using Microsoft.Extensions.Logging;
using SharedSubsystem.DTOs.IntegrationEvents;
using SharedSubsystem.Handlers.Abstractions;

namespace DataSyncSubsystem.Handlers;

public class UserRegisteredDataSyncHandler : IEventHandler<UserRegisteredEvent>
{
    private readonly ILogger<UserRegisteredDataSyncHandler> _logger;

    public UserRegisteredDataSyncHandler(ILogger<UserRegisteredDataSyncHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(UserRegisteredEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[DataSync] UserRegisteredEvent handled: UserId={UserId}, Username='{Username}', Email='{Email}', UserType='{UserType}', KeycloakId='{KeycloakId}'",
            @event.UserId, @event.Username, @event.Email, @event.UserType, @event.KeycloakId);

        return Task.CompletedTask;
    }
}

public class UserLoggedInDataSyncHandler : IEventHandler<UserLoggedInEvent>
{
    private readonly ILogger<UserLoggedInDataSyncHandler> _logger;

    public UserLoggedInDataSyncHandler(ILogger<UserLoggedInDataSyncHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(UserLoggedInEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[DataSync] UserLoggedInEvent handled: Username='{Username}' at {LoginTime}",
            @event.Username, @event.OccurredOn);

        return Task.CompletedTask;
    }
}

public class UserLoggedOutDataSyncHandler : IEventHandler<UserLoggedOutEvent>
{
    private readonly ILogger<UserLoggedOutDataSyncHandler> _logger;

    public UserLoggedOutDataSyncHandler(ILogger<UserLoggedOutDataSyncHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(UserLoggedOutEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[DataSync] UserLoggedOutEvent handled: Username='{Username}' at {LogoutTime}",
            @event.Username, @event.OccurredOn);

        return Task.CompletedTask;
    }
}
