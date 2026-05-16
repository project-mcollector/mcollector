using Identity.Api.Api.Controllers;
using Identity.Api.Api.Requests;
using Identity.Api.Application.Services;
using Identity.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Utilities;

namespace Identity.Api.Tests;

public class AuthControllerTests
{
    private static AuthController CreateController(Mock<IAuthService> authService, string? userId = null)
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null, null, null, null, null, null, null, null);

        var controller = new AuthController(authService.Object, userManager.Object);
        if (userId is not null)
            controller.ControllerContext = TestHelpers.ControllerContextFor(userId);
        return controller;
    }

    // Login

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.LoginAsync("user@acme.dev", "bad-password", CancellationToken.None))
            .ReturnsAsync(Errors.Unauthorized("user@acme.dev"));

        var controller = CreateController(authService);
        var result = await controller.Login(
            new() { Email = "user@acme.dev", Password = "bad-password" },
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Login_UnconfirmedEmail_ReturnsForbidden()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.LoginAsync("user@acme.dev", "Password1!", CancellationToken.None))
            .ReturnsAsync(Errors.EmailNotConfirmed());

        var controller = CreateController(authService);
        var result = await controller.Login(
            new() { Email = "user@acme.dev", Password = "Password1!" },
            CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsJwt()
    {
        var token = new AuthTokenDto("test.jwt.token", 3600, "test-refresh-token");
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.LoginAsync("user@acme.dev", "password123", CancellationToken.None))
            .ReturnsAsync(token);

        var controller = CreateController(authService);
        var result = await controller.Login(
            new() { Email = "user@acme.dev", Password = "password123" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthTokenDto>(ok.Value);
        Assert.Equal("test.jwt.token", response.AccessToken);
        Assert.Equal("test-refresh-token", response.RefreshToken);
    }

    // Register

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.RegisterAsync("dup@acme.dev", It.IsAny<string>(), CancellationToken.None))
            .ReturnsAsync(Errors.Validation("Registration", "Email already taken"));

        var controller = CreateController(authService);
        var result = await controller.Register(
            new() { Email = "dup@acme.dev", Password = "Password1!" },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Register_Success_ReturnsCreated()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.RegisterAsync("new@acme.dev", "Password1!", CancellationToken.None))
            .ReturnsAsync(Result.Success());

        var controller = CreateController(authService);
        var result = await controller.Register(
            new() { Email = "new@acme.dev", Password = "Password1!" },
            CancellationToken.None);

        Assert.IsType<CreatedResult>(result);
    }

    // Refresh

    [Fact]
    public async Task Refresh_ValidToken_ReturnsOkWithJwt()
    {
        var token = new AuthTokenDto("new.jwt.token", 3600, "new-refresh-token");
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.RefreshAsync("valid-refresh", CancellationToken.None))
            .ReturnsAsync(token);

        var controller = CreateController(authService);
        var result = await controller.Refresh(
            new RefreshRequest { RefreshToken = "valid-refresh" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthTokenDto>(ok.Value);
        Assert.Equal("new.jwt.token", response.AccessToken);
    }

    [Fact]
    public async Task Refresh_InvalidToken_ReturnsUnauthorized()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.RefreshAsync("expired-token", CancellationToken.None))
            .ReturnsAsync(Errors.Unauthorized("Invalid or expired refresh token"));

        var controller = CreateController(authService);
        var result = await controller.Refresh(
            new RefreshRequest { RefreshToken = "expired-token" },
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // ConfirmEmail

    [Fact]
    public async Task ConfirmEmail_Success_ReturnsOkWithToken()
    {
        var token = new AuthTokenDto("access.token", 3600, "refresh.token");
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.ConfirmEmail("user-id", "valid-token", CancellationToken.None))
            .ReturnsAsync(token);

        var controller = CreateController(authService);
        var result = await controller.ConfirmEmail(
            new ConfirmEmailRequest { UserId = "user-id", Token = "valid-token" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<AuthTokenDto>(ok.Value);
    }

    [Fact]
    public async Task ConfirmEmail_InvalidToken_ReturnsBadRequest()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.ConfirmEmail("user-id", "bad-token", CancellationToken.None))
            .ReturnsAsync(Errors.Validation("Email Confirmation", "Invalid token"));

        var controller = CreateController(authService);
        var result = await controller.ConfirmEmail(
            new ConfirmEmailRequest { UserId = "user-id", Token = "bad-token" },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ResendConfirmation

    [Fact]
    public async Task ResendConfirmation_AlwaysReturnsNoContent()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.ResendConfirmationEmailAsync("user@acme.dev", CancellationToken.None))
            .ReturnsAsync(Result.Success());

        var controller = CreateController(authService);
        var result = await controller.ResendConfirmation(
            new ResendConfirmationRequest { Email = "user@acme.dev" },
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    // ForgotPassword

    [Fact]
    public async Task ForgotPassword_AlwaysReturnsNoContent()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.ForgotPasswordAsync("user@acme.dev", CancellationToken.None))
            .ReturnsAsync(Result.Success());

        var controller = CreateController(authService);
        var result = await controller.ForgotPassword(
            new ForgotPasswordRequest { Email = "user@acme.dev" },
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    // ResetPassword

    [Fact]
    public async Task ResetPassword_Success_ReturnsNoContent()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.ResetPasswordAsync("user-id", "valid-token", "NewPassword1!", CancellationToken.None))
            .ReturnsAsync(Result.Success());

        var controller = CreateController(authService);
        var result = await controller.ResetPassword(
            new ResetPasswordRequest { UserId = "user-id", Token = "valid-token", Password = "NewPassword1!" },
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task ResetPassword_InvalidLink_ReturnsBadRequest()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.ResetPasswordAsync("bad-id", "bad-token", "NewPassword1!", CancellationToken.None))
            .ReturnsAsync(Errors.Validation("ResetPassword", "Invalid reset link"));

        var controller = CreateController(authService);
        var result = await controller.ResetPassword(
            new ResetPasswordRequest { UserId = "bad-id", Token = "bad-token", Password = "NewPassword1!" },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // Logout

    [Fact]
    public async Task Logout_AlwaysReturnsNoContent()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.RevokeAsync("any-token", CancellationToken.None))
            .Returns(Task.CompletedTask);

        var controller = CreateController(authService);
        var result = await controller.Logout(
            new RevokeRequest { RefreshToken = "any-token" },
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    // LogoutOther

    [Fact]
    public async Task LogoutOther_AuthenticatedUser_ReturnsNoContent()
    {
        var userId = Guid.NewGuid().ToString();
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.RevokeOtherSessionsAsync("current-token", userId, CancellationToken.None))
            .Returns(Task.CompletedTask);

        var controller = CreateController(authService, userId);
        var result = await controller.LogoutOther(
            new RevokeRequest { RefreshToken = "current-token" },
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    // GetPasskeyOptions — empty email uses discoverable flow

    [Fact]
    public async Task GetPasskeyOptions_EmptyEmail_UsesDiscoverableFlow()
    {
        const string optionsJson = "{\"challenge\":\"disc123\"}";
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.BeginDiscoverablePasskeyLoginAsync())
            .ReturnsAsync(optionsJson);

        var controller = CreateController(authService);
        var result = await controller.GetPasskeyOptions(new PasskeyOptionsRequest { Email = null });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(optionsJson, ok.Value);
        authService.Verify(x => x.BeginDiscoverablePasskeyLoginAsync(), Times.Once);
        authService.Verify(x => x.BeginPasskeyLoginAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetPasskeyOptions_EmptyStringEmail_UsesDiscoverableFlow()
    {
        const string optionsJson = "{\"challenge\":\"disc456\"}";
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.BeginDiscoverablePasskeyLoginAsync())
            .ReturnsAsync(optionsJson);

        var controller = CreateController(authService);
        var result = await controller.GetPasskeyOptions(new PasskeyOptionsRequest { Email = string.Empty });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(optionsJson, ok.Value);
        authService.Verify(x => x.BeginDiscoverablePasskeyLoginAsync(), Times.Once);
    }
}

public class ApiKeyServiceTests
{
    private readonly ApiKeyService _service = new();

    [Fact]
    public void GenerateApiKey_HasCorrectPrefix()
    {
        var key = _service.GenerateApiKey();
        Assert.StartsWith("proj_", key);
    }

    [Fact]
    public void GenerateApiKey_HasContentBeyondPrefix()
    {
        var key = _service.GenerateApiKey();
        // "proj_" (5 chars) + base64url(32 bytes) — key must be longer than prefix
        Assert.True(key.Length > 5);
    }

    [Fact]
    public void GenerateApiKey_IsUrlSafe()
    {
        for (var i = 0; i < 50; i++)
        {
            var key = _service.GenerateApiKey();
            // Standard base64 characters '+' and '/' must be replaced
            Assert.DoesNotContain("+", key);
            Assert.DoesNotContain("/", key);
            // Padding must be stripped
            Assert.DoesNotContain("=", key);
        }
    }

    [Fact]
    public void GenerateApiKey_ProducesUniqueKeys()
    {
        var keys = Enumerable.Range(0, 100).Select(_ => _service.GenerateApiKey()).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void GenerateApiKey_SuffixLengthIsCorrect()
    {
        // 32 random bytes → base64url → 43 chars (padding stripped), so total = 48
        var key = _service.GenerateApiKey();
        var suffix = key["proj_".Length..];
        // Base64url of 32 bytes: ceil(32 * 4 / 3) = 44, minus up to 2 padding chars = 42 or 43
        Assert.InRange(suffix.Length, 42, 44);
    }
}
