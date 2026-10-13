using DataSyncSubsystem.Handlers;
using Microsoft.Extensions.DependencyInjection;
using SharedSubsystem.DTOs.IntegrationEvents;
using SharedSubsystem.Handlers.Abstractions;

namespace DataSyncSubsystem.APIs.Extensions;

public static class ServiceCollectionExtensions
{
    // DataSync has no host of its own: whichever host raises these events registers the handlers in-process.
    public static IServiceCollection AddDataSyncSubsystem(this IServiceCollection services)
    {
        services.AddScoped<IEventHandler<UserRegisteredEvent>, UserRegisteredDataSyncHandler>();
        services.AddScoped<IEventHandler<UserLoggedInEvent>, UserLoggedInDataSyncHandler>();
        return services;
    }
}
