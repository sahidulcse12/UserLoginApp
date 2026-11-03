using Microsoft.Extensions.Logging;

namespace ServiceBus.Handlers;

public class InMemoryServiceBus : ServiceBus
{
    public InMemoryServiceBus(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public InMemoryServiceBus(IServiceProvider serviceProvider, ILogger<ServiceBus> logger) : base(serviceProvider, logger)
    {
    }
}
