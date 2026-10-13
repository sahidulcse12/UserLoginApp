using Microsoft.Extensions.DependencyInjection;
using SharedSubsystem.Handlers.Abstractions;

namespace ServiceBus.Handlers;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServiceBus(this IServiceCollection services)
        => services.AddScoped<IServiceBus, InMemoryServiceBus>();
}
