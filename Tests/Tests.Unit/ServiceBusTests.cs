using Moq;
using ServiceBus.Handlers;
using SharedSubsystem.DTOs.Abstractions;
using SharedSubsystem.Handlers.Abstructions;
using SharedSubsystem.Handlers.Events;
using Xunit;

namespace Tests.Unit.ServiceBusTests;

public record TestCommand(string Name) : ICommand;
public record TestQuery(int Id) : IQuery<string>;

public class ServiceBusTests
{
    [Fact]
    public async Task SendCommandAsync_WhenHandlerExists_ReturnsEvents()
    {
        // Arrange
        var command = new TestCommand("Test");
        var expectedEvents = EventFactory.CreateSuccessEvent(EventId.ResourceCreated, "Created");

        var handlerMock = new Mock<ICommandHandler<TestCommand>>();
        handlerMock.Setup(h => h.HandleAsync(command))
                   .ReturnsAsync(expectedEvents);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ICommandHandler<TestCommand>)))
                           .Returns(handlerMock.Object);

        var serviceBus = new ServiceBus.Handlers.ServiceBus(serviceProviderMock.Object);

        // Act
        var result = await serviceBus.SendCommandAsync(command);

        // Assert
        Assert.NotNull(result);
        var eventList = result.ToList();
        Assert.Single(eventList);
        Assert.Equal(EventType.Success, eventList[0].EventType);
        Assert.Equal(EventId.ResourceCreated, eventList[0].EventId);
    }

    [Fact]
    public async Task SendCommandAsync_WhenHandlerMissing_ReturnsNull()
    {
        // Arrange
        var command = new TestCommand("Test");
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ICommandHandler<TestCommand>)))
                           .Returns(null!);

        var serviceBus = new ServiceBus.Handlers.ServiceBus(serviceProviderMock.Object);

        // Act
        var result = await serviceBus.SendCommandAsync(command);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SendQueryAsync_WhenHandlerExists_ReturnsResult()
    {
        // Arrange
        var query = new TestQuery(42);
        var expectedResult = "ResultData";

        var handlerMock = new Mock<IQueryHandler<TestQuery, string>>();
        handlerMock.Setup(h => h.HandleAsync(query))
                   .ReturnsAsync(expectedResult);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IQueryHandler<TestQuery, string>)))
                           .Returns(handlerMock.Object);

        var serviceBus = new ServiceBus.Handlers.ServiceBus(serviceProviderMock.Object);

        // Act
        var result = await serviceBus.SendQueryAsync<TestQuery, string>(query);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task SendQueryAsync_WhenHandlerMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var query = new TestQuery(42);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IQueryHandler<TestQuery, string>)))
                           .Returns(null!);

        var serviceBus = new ServiceBus.Handlers.ServiceBus(serviceProviderMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            serviceBus.SendQueryAsync<TestQuery, string>(query));
    }
}
