using Microsoft.Extensions.DependencyInjection;
using SharedSubsystem.Handlers.Abstructions;

namespace ServiceBus.Handlers;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServiceBus(this IServiceCollection services)
        => services.AddScoped<IServiceBus, ServiceBus>();
}
