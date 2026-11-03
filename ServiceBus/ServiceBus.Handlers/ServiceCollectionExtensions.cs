using Microsoft.Extensions.DependencyInjection;

namespace ServiceBus.Handlers;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServiceBus(this IServiceCollection services)
        => services.AddScoped<IServiceBus, ServiceBus>();
}
