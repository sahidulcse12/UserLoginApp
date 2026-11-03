using AuthSubsystem.APIs.Controllers;
using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ServiceBus.Handlers;
using SharedSubsystem.Handlers.Events;
using Xunit;

namespace Tests.Unit.Controllers;

public class RolesControllerTests
{
    private readonly Mock<IServiceBus> _serviceBusMock;
    private readonly RolesController _controller;

    public RolesControllerTests()
    {
        _serviceBusMock = new Mock<IServiceBus>();
        _controller = new RolesController(_serviceBusMock.Object);
    }

    [Fact]
    public async Task GetAllUsers_WhenUsersExist_ReturnsOkWithSuccessEvent()
    {
        // Arrange
        var users = new List<KeycloakUserRoleDto>
        {
            new KeycloakUserRoleDto { Id = "1", Username = "user1", Roles = new List<string> { "admin" } }
        };

        _serviceBusMock.Setup(s => s.SendQueryAsync<GetAllUsersWithRolesQuery, IEnumerable<KeycloakUserRoleDto>>(It.IsAny<GetAllUsersWithRolesQuery>()))
                       .ReturnsAsync(users);

        // Act
        var result = await _controller.GetAllUsers(null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var events = Assert.IsAssignableFrom<IEnumerable<Event>>(okResult.Value).ToList();
        Assert.Single(events);
        Assert.Equal(EventType.Success, events[0].EventType);
        Assert.Equal(EventId.ResourceRetrieved, events[0].EventId);
    }

    [Fact]
    public async Task GetUserPermissions_WhenUserNotFound_ReturnsOkWithNotFoundEvent()
    {
        // Arrange
        _serviceBusMock.Setup(s => s.SendQueryAsync<GetUserPermissionsQuery, UserPermissionsDto?>(It.IsAny<GetUserPermissionsQuery>()))
                       .ReturnsAsync((UserPermissionsDto?)null);

        // Act
        var result = await _controller.GetUserPermissions("unknown_user");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var events = Assert.IsAssignableFrom<IEnumerable<Event>>(okResult.Value).ToList();
        Assert.Single(events);
        Assert.Equal(EventType.Fail, events[0].EventType);
        Assert.Equal(EventId.ResourceNotFound, events[0].EventId);
    }

    [Fact]
    public async Task AssignRole_ReturnsOk_WithEvents()
    {
        // Arrange
        var request = new AssignRoleRequest { Username = "user1", Role = "admin" };
        var expectedEvents = EventFactory.CreateSuccessEvent(EventId.ResourceUpdated, new UserPermissionsDto { Username = "user1" });

        _serviceBusMock.Setup(s => s.SendCommandAsync(It.IsAny<AssignRoleCommand>()))
                       .ReturnsAsync(expectedEvents);

        // Act
        var result = await _controller.AssignRole(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedEvents, okResult.Value);
    }
}
