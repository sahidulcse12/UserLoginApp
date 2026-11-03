namespace ServiceBus.Handlers;

public class InMemoryServiceBus : ServiceBus
{
    public InMemoryServiceBus(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }
}
