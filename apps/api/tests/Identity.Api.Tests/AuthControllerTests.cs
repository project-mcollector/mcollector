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
        authService.Setup(x => x.LoginAsync("user@acme.dev", "bad-password"))
            .ReturnsAsync(Errors.Unauthorized("user@acme.dev"));

        var controller = new AuthController(authService.Object);
        var result = await controller.Login(new() { Email = "user@acme.dev", Password = "bad-password" });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid credentials", unauthorized.Value);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsJwt()
    {
        var token = new AuthTokenDto("test.jwt.token", 604800);
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.LoginAsync("user@acme.dev", "password123"))
            .ReturnsAsync(token);

        var controller = new AuthController(authService.Object);
        var result = await controller.Login(new() { Email = "user@acme.dev", Password = "password123" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthTokenDto>(ok.Value);
        Assert.Equal("test.jwt.token", response.AccessToken);
        Assert.Equal(604800, response.ExpiresIn);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.RegisterAsync("dup@acme.dev", It.IsAny<string>()))
            .ReturnsAsync(Errors.Validation("Registration", "Email already taken"));

        var controller = new AuthController(authService.Object);
        var result = await controller.Register(new() { Email = "dup@acme.dev", Password = "Password1!" });

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
