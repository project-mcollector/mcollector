using Identity.Api.Api.Controllers;
using Identity.Api.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Utilities;

namespace Identity.Api.Tests;

public class AuthControllerTests
{
    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.LoginAsync("user@acme.dev", "bad-password", default))
            .ReturnsAsync(Errors.Unauthorized("user@acme.dev"));

        var controller = new AuthController(authService.Object);
        var result = await controller.Login(
            new() { Email = "user@acme.dev", Password = "bad-password" },
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Login_UnconfirmedEmail_ReturnsForbidden()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.LoginAsync("user@acme.dev", "Password1!", default))
            .ReturnsAsync(Errors.EmailNotConfirmed());

        var controller = new AuthController(authService.Object);
        var result = await controller.Login(
            new() { Email = "user@acme.dev", Password = "Password1!" },
            CancellationToken.None);

        var statusResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsJwt()
    {
        var token = new AuthTokenDto("test.jwt.token", 3600, "test-refresh-token");
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.LoginAsync("user@acme.dev", "password123", default))
            .ReturnsAsync(token);

        var controller = new AuthController(authService.Object);
        var result = await controller.Login(
            new() { Email = "user@acme.dev", Password = "password123" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthTokenDto>(ok.Value);
        Assert.Equal("test.jwt.token", response.AccessToken);
        Assert.Equal("test-refresh-token", response.RefreshToken);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.RegisterAsync("dup@acme.dev", It.IsAny<string>(), default))
            .ReturnsAsync(Errors.Validation("Registration", "Email already taken"));

        var controller = new AuthController(authService.Object);
        var result = await controller.Register(
            new() { Email = "dup@acme.dev", Password = "Password1!" },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
