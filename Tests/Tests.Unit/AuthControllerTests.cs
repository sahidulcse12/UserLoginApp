using AuthSubsystem.APIs.Controllers;
using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ServiceBus.Handlers;
using SharedSubsystem.Handlers.Events;
using Xunit;

namespace Tests.Unit.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IServiceBus> _serviceBusMock;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _serviceBusMock = new Mock<IServiceBus>();
        _controller = new AuthController(_serviceBusMock.Object);
    }

    [Fact]
    public async Task Register_ReturnsOk_WithEvents()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "testuser",
            Password = "Password123!",
            Email = "test@example.com",
            UserType = "mis"
        };
        var expectedEvents = EventFactory.CreateSuccessEvent(EventId.ResourceCreated, new RegisterResponse { Username = "testuser" });
        _serviceBusMock.Setup(s => s.SendCommandAsync(It.IsAny<RegisterUserCommand>()))
                       .ReturnsAsync(expectedEvents);

        // Act
        var result = await _controller.Register(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedEvents, okResult.Value);
    }

    [Fact]
    public async Task Login_ReturnsOk_WithEvents()
    {
        // Arrange
        var request = new LoginRequest { Username = "testuser", Password = "Password123!" };
        var expectedEvents = EventFactory.CreateSuccessEvent(EventId.UserAuthenticated, new TokenResponse { AccessToken = "abc" });
        _serviceBusMock.Setup(s => s.SendCommandAsync(It.IsAny<LoginUserCommand>()))
                       .ReturnsAsync(expectedEvents);

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedEvents, okResult.Value);
    }

    [Fact]
    public async Task RefreshToken_ReturnsOk_WithEvents()
    {
        // Arrange
        var request = new RefreshTokenRequest { RefreshToken = "valid-refresh" };
        var expectedEvents = EventFactory.CreateSuccessEvent(EventId.UserAuthenticated, new TokenResponse { AccessToken = "new-token" });
        _serviceBusMock.Setup(s => s.SendCommandAsync(It.IsAny<RefreshTokenCommand>()))
                       .ReturnsAsync(expectedEvents);

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedEvents, okResult.Value);
    }

    [Fact]
    public async Task Logout_ReturnsOk_WithEvents()
    {
        // Arrange
        var request = new LogoutRequest { RefreshToken = "valid-refresh" };
        var expectedEvents = EventFactory.CreateSuccessEvent(EventId.UserLoggedOut, new LogoutResponse());
        _serviceBusMock.Setup(s => s.SendCommandAsync(It.IsAny<LogoutUserCommand>()))
                       .ReturnsAsync(expectedEvents);

        // Act
        var result = await _controller.Logout(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedEvents, okResult.Value);
    }
}
