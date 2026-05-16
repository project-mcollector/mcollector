using Identity.Api.Api.Controllers;
using Identity.Api.Api.Requests;
using Identity.Api.Application.Services;
using Identity.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Utilities;
using Result = Utilities.Result;

namespace Identity.Api.Tests;

public class PasskeyControllerTests
{
    private static readonly string TestUserId = Guid.NewGuid().ToString();

    private static (AuthController Controller, Mock<UserManager<ApplicationUser>> UserManager)
        CreateController(Mock<IAuthService> authService, string? userId = null)
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null, null, null, null, null, null, null, null);

        var controller = new AuthController(authService.Object, userManager.Object)
        {
            ControllerContext = userId is not null
                ? TestHelpers.ControllerContextFor(userId)
                : TestHelpers.AnonymousControllerContext()
        };

        return (controller, userManager);
    }

    // GetPasskeyOptions

    [Fact]
    public async Task GetPasskeyOptions_UnknownEmail_ReturnsBadRequest()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.BeginPasskeyLoginAsync("unknown@acme.dev"))
            .ReturnsAsync(Errors.NotFound("User", "unknown@acme.dev"));

        var (controller, _) = CreateController(authService);
        var result = await controller.GetPasskeyOptions(new PasskeyOptionsRequest { Email = "unknown@acme.dev" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetPasskeyOptions_KnownEmail_ReturnsOkWithOptionsJson()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.BeginPasskeyLoginAsync("user@acme.dev"))
            .ReturnsAsync("{\"challenge\":\"abc123\"}");

        var (controller, _) = CreateController(authService);
        var result = await controller.GetPasskeyOptions(new PasskeyOptionsRequest { Email = "user@acme.dev" });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("{\"challenge\":\"abc123\"}", ok.Value);
    }

    // PasskeyLogin

    [Fact]
    public async Task PasskeyLogin_InvalidCredential_ReturnsUnauthorized()
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.CompletePasskeyLoginAsync("bad-cred"))
            .ReturnsAsync(Errors.Unauthorized("bad-cred"));

        var (controller, _) = CreateController(authService);
        var result = await controller.PasskeyLogin(new PasskeyCredentialRequest { CredentialJson = "bad-cred" });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User 'bad-cred' is not authorized to perform this action", unauthorized.Value);
    }

    [Fact]
    public async Task PasskeyLogin_ValidCredential_ReturnsOkWithToken()
    {
        var token = new AuthTokenDto("jwt.access.token", 3600, "refresh-token");
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.CompletePasskeyLoginAsync("valid-cred"))
            .ReturnsAsync(token);

        var (controller, _) = CreateController(authService);
        var result = await controller.PasskeyLogin(new PasskeyCredentialRequest { CredentialJson = "valid-cred" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<AuthTokenDto>(ok.Value);
        Assert.Equal("jwt.access.token", dto.AccessToken);
        Assert.Equal("refresh-token", dto.RefreshToken);
    }

    // GetPasskeys

    [Fact]
    public async Task GetPasskeys_NoClaimsPrincipal_ReturnsUnauthorized()
    {
        var (controller, _) = CreateController(new Mock<IAuthService>());

        var result = await controller.GetPasskeys();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetPasskeys_AuthenticatedUser_ReturnsOkWithPasskeys()
    {
        var user = new ApplicationUser { Id = TestUserId, Email = "user@acme.dev" };
        var passkeys = new List<PasskeyDto>
        {
            new("AQID", DateTimeOffset.UtcNow, ["internal"], IsBackupEligible: false, Aaguid: null)
        };

        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.GetPasskeysAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(passkeys);

        var (controller, userManager) = CreateController(authService, userId: TestUserId);
        userManager.Setup(x => x.FindByIdAsync(TestUserId)).ReturnsAsync(user);

        var result = await controller.GetPasskeys();

        var ok = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsAssignableFrom<IReadOnlyList<PasskeyDto>>(ok.Value);
        Assert.Single(value);
        Assert.Equal("AQID", value[0].CredentialId);
    }

    [Fact]
    public async Task GetPasskeys_ServiceFailure_ReturnsInternalServerError()
    {
        var user = new ApplicationUser { Id = TestUserId };
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.GetPasskeysAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(Errors.Internal("storage failure"));

        var (controller, userManager) = CreateController(authService, userId: TestUserId);
        userManager.Setup(x => x.FindByIdAsync(TestUserId)).ReturnsAsync(user);

        var result = await controller.GetPasskeys();

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, obj.StatusCode);
    }

    // DeletePasskey

    [Fact]
    public async Task DeletePasskey_NoClaimsPrincipal_ReturnsUnauthorized()
    {
        var (controller, _) = CreateController(new Mock<IAuthService>());

        var result = await controller.DeletePasskey("AQID");

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task DeletePasskey_ExistingCredential_ReturnsNoContent()
    {
        var user = new ApplicationUser { Id = TestUserId };
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.DeletePasskeyAsync(It.IsAny<ApplicationUser>(), "AQID"))
            .ReturnsAsync(Result.Success());

        var (controller, userManager) = CreateController(authService, userId: TestUserId);
        userManager.Setup(x => x.FindByIdAsync(TestUserId)).ReturnsAsync(user);

        var result = await controller.DeletePasskey("AQID");

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeletePasskey_NotFound_Returns404()
    {
        var user = new ApplicationUser { Id = TestUserId };
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.DeletePasskeyAsync(It.IsAny<ApplicationUser>(), "AQID"))
            .ReturnsAsync(Errors.NotFound("Passkey", "AQID"));

        var (controller, userManager) = CreateController(authService, userId: TestUserId);
        userManager.Setup(x => x.FindByIdAsync(TestUserId)).ReturnsAsync(user);

        var result = await controller.DeletePasskey("AQID");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // GetPasskeyRegistrationOptions

    [Fact]
    public async Task GetPasskeyRegistrationOptions_NoClaimsPrincipal_ReturnsUnauthorized()
    {
        var (controller, _) = CreateController(new Mock<IAuthService>());

        var result = await controller.GetPasskeyRegistrationOptions();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetPasskeyRegistrationOptions_UserNotInStore_ReturnsUnauthorized()
    {
        var authService = new Mock<IAuthService>();
        var (controller, userManager) = CreateController(authService, userId: TestUserId);
        userManager.Setup(x => x.FindByIdAsync(TestUserId))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await controller.GetPasskeyRegistrationOptions();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetPasskeyRegistrationOptions_AuthenticatedUser_ReturnsOkWithOptionsJson()
    {
        var user = new ApplicationUser { Id = TestUserId, Email = "user@acme.dev" };
        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.BeginPasskeyRegistrationAsync(user))
            .ReturnsAsync("{\"challenge\":\"reg123\"}");

        var (controller, userManager) = CreateController(authService, userId: TestUserId);
        userManager.Setup(x => x.FindByIdAsync(TestUserId)).ReturnsAsync(user);

        var result = await controller.GetPasskeyRegistrationOptions();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("{\"challenge\":\"reg123\"}", ok.Value);
    }

    // RegisterPasskey

    [Fact]
    public async Task RegisterPasskey_NoClaimsPrincipal_ReturnsUnauthorized()
    {
        var (controller, _) = CreateController(new Mock<IAuthService>());

        var result = await controller.RegisterPasskey(new PasskeyCredentialRequest { CredentialJson = "cred" });

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task RegisterPasskey_UserNotInStore_ReturnsUnauthorized()
    {
        var authService = new Mock<IAuthService>();
        var (controller, userManager) = CreateController(authService, userId: TestUserId);
        userManager.Setup(x => x.FindByIdAsync(TestUserId))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await controller.RegisterPasskey(new PasskeyCredentialRequest { CredentialJson = "cred" });

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task RegisterPasskey_AttestationFailed_ReturnsBadRequestWithMessage()
    {
        var user = new ApplicationUser { Id = TestUserId };

        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.CompletePasskeyRegistrationAsync(user, "bad-cred"))
            .ReturnsAsync(Errors.Validation("Passkey", "invalid credential"));

        var (controller, userManager) = CreateController(authService, userId: TestUserId);
        userManager.Setup(x => x.FindByIdAsync(TestUserId)).ReturnsAsync(user);

        var result = await controller.RegisterPasskey(new PasskeyCredentialRequest { CredentialJson = "bad-cred" });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("invalid credential", bad.Value);
    }

    [Fact]
    public async Task RegisterPasskey_AttestationSucceeded_ReturnsOk()
    {
        var user = new ApplicationUser { Id = TestUserId };

        var authService = new Mock<IAuthService>();
        authService.Setup(x => x.CompletePasskeyRegistrationAsync(user, "valid-cred"))
            .ReturnsAsync(Result.Success());

        var (controller, userManager) = CreateController(authService, userId: TestUserId);
        userManager.Setup(x => x.FindByIdAsync(TestUserId)).ReturnsAsync(user);

        var result = await controller.RegisterPasskey(new PasskeyCredentialRequest { CredentialJson = "valid-cred" });

        Assert.IsType<OkResult>(result);
    }
}
