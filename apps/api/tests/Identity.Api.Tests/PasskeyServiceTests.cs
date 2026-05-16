using Identity.Api.Application.Services;
using Identity.Api.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Identity.Api.Tests;

public class PasskeyServiceTests
{
    private static (
        Mock<UserManager<ApplicationUser>> Um,
        Mock<SignInManager<ApplicationUser>> Sm,
        Mock<ITokenService> Ts)
        CreateMocks()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var um = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null, null, null, null, null, null, null, null);

        var sm = new Mock<SignInManager<ApplicationUser>>(
            um.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
            Options.Create(new IdentityOptions()),
            new Mock<ILogger<SignInManager<ApplicationUser>>>().Object,
            new Mock<IAuthenticationSchemeProvider>().Object,
            new Mock<IUserConfirmation<ApplicationUser>>().Object);

        var ts = new Mock<ITokenService>();
        return (um, sm, ts);
    }

    private static ApplicationUser CreateUser() => new()
    {
        Id = Guid.NewGuid().ToString(),
        Email = "user@acme.dev",
        UserName = "user@acme.dev"
    };

    private static UserPasskeyInfo CreateDummyPasskeyInfo() => new(
        credentialId: [1, 2, 3],
        publicKey: [4, 5, 6],
        createdAt: DateTimeOffset.UtcNow,
        signCount: 0,
        transports: [],
        isUserVerified: true,
        isBackupEligible: false,
        isBackedUp: false,
        clientDataJson: [7, 8, 9],
        attestationObject: [10, 11, 12]);

    // BeginLoginAsync

    [Fact]
    public async Task BeginLoginAsync_UnknownEmail_ReturnsNotFoundError()
    {
        var (um, sm, ts) = CreateMocks();
        um.Setup(x => x.FindByEmailAsync("ghost@acme.dev"))
            .ReturnsAsync((ApplicationUser?)null);

        var service = new PasskeyService(um.Object, sm.Object, ts.Object);
        var result = await service.BeginLoginAsync("ghost@acme.dev");

        Assert.False(result.IsSuccess);
        Assert.Equal("User.NotFound", result.Error?.Id);
    }

    [Fact]
    public async Task BeginLoginAsync_KnownUser_DelegatesToSignInManagerAndReturnsOptions()
    {
        var user = CreateUser();
        const string optionsJson = "{\"challenge\":\"abc\"}";

        var (um, sm, ts) = CreateMocks();
        um.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        sm.Setup(x => x.MakePasskeyRequestOptionsAsync(user)).ReturnsAsync(optionsJson);

        var service = new PasskeyService(um.Object, sm.Object, ts.Object);
        var result = await service.BeginLoginAsync(user.Email);

        Assert.True(result.IsSuccess);
        Assert.Equal(optionsJson, result.Value);
    }

    // CompleteLoginAsync

    [Fact]
    public async Task CompleteLoginAsync_AssertionFails_ReturnsUnauthorizedError()
    {
        var (um, sm, ts) = CreateMocks();
        var failedAssertion = PasskeyAssertionResult.Fail<ApplicationUser>(
            new PasskeyException("bad credential"));
        sm.Setup(x => x.PerformPasskeyAssertionAsync("bad-cred"))
            .ReturnsAsync(failedAssertion);

        var service = new PasskeyService(um.Object, sm.Object, ts.Object);
        var result = await service.CompleteLoginAsync("bad-cred");

        Assert.False(result.IsSuccess);
        Assert.Equal("Unauthorized", result.Error?.Id);
    }

    [Fact]
    public async Task CompleteLoginAsync_AssertionSucceeds_ReturnsTokenFromTokenService()
    {
        var user = CreateUser();
        var passkey = CreateDummyPasskeyInfo();
        var successfulAssertion = PasskeyAssertionResult.Success(passkey, user);
        var expectedToken = new AuthTokenDto("access.token", 3600, "refresh.token");

        var (um, sm, ts) = CreateMocks();
        sm.Setup(x => x.PerformPasskeyAssertionAsync("valid-cred"))
            .ReturnsAsync(successfulAssertion);
        ts.Setup(x => x.CreateTokenAsync(user, default)).ReturnsAsync(expectedToken);

        var service = new PasskeyService(um.Object, sm.Object, ts.Object);
        var result = await service.CompleteLoginAsync("valid-cred");

        Assert.True(result.IsSuccess);
        Assert.Equal("access.token", result.Value.AccessToken);
        Assert.Equal("refresh.token", result.Value.RefreshToken);
    }

    // BeginRegistrationAsync

    [Fact]
    public async Task BeginRegistrationAsync_BuildsCorrectUserEntityAndDelegatesToSignInManager()
    {
        var user = CreateUser();
        const string optionsJson = "{\"challenge\":\"reg\"}";

        var (um, sm, ts) = CreateMocks();
        sm.Setup(x => x.MakePasskeyCreationOptionsAsync(It.Is<PasskeyUserEntity>(e =>
                e.Id == user.Id &&
                e.Name == user.UserName &&
                e.DisplayName == user.Email)))
            .ReturnsAsync(optionsJson);

        var service = new PasskeyService(um.Object, sm.Object, ts.Object);
        var result = await service.BeginRegistrationAsync(user);

        Assert.True(result.IsSuccess);
        Assert.Equal(optionsJson, result.Value);
    }

    [Fact]
    public async Task BeginRegistrationAsync_UserWithoutUserName_FallsBackToEmailForName()
    {
        var user = new ApplicationUser { Id = "id1", Email = "fallback@acme.dev", UserName = null };
        const string optionsJson = "{\"challenge\":\"reg\"}";

        var (um, sm, ts) = CreateMocks();
        sm.Setup(x => x.MakePasskeyCreationOptionsAsync(It.Is<PasskeyUserEntity>(e =>
                e.Id == "id1" &&
                e.Name == "fallback@acme.dev" &&
                e.DisplayName == "fallback@acme.dev")))
            .ReturnsAsync(optionsJson);

        var service = new PasskeyService(um.Object, sm.Object, ts.Object);
        var result = await service.BeginRegistrationAsync(user);

        Assert.True(result.IsSuccess);
    }

    // CompleteRegistrationAsync

    [Fact]
    public async Task CompleteRegistrationAsync_AttestationFailed_DoesNotCallAddOrUpdatePasskey()
    {
        var user = CreateUser();
        var failedAttestation = PasskeyAttestationResult.Fail(new PasskeyException("bad json"));

        var (um, sm, ts) = CreateMocks();
        sm.Setup(x => x.PerformPasskeyAttestationAsync("bad-cred"))
            .ReturnsAsync(failedAttestation);

        var service = new PasskeyService(um.Object, sm.Object, ts.Object);
        var result = await service.CompleteRegistrationAsync(user, "bad-cred");

        Assert.False(result.IsSuccess);
        um.Verify(x => x.AddOrUpdatePasskeyAsync(It.IsAny<ApplicationUser>(), It.IsAny<UserPasskeyInfo>()), Times.Never);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_AttestationSucceeded_CallsAddOrUpdatePasskey()
    {
        var user = CreateUser();
        var passkey = CreateDummyPasskeyInfo();
        var userEntity = new PasskeyUserEntity { Id = user.Id, Name = user.UserName, DisplayName = user.Email };
        var successfulAttestation = PasskeyAttestationResult.Success(passkey, userEntity);

        var (um, sm, ts) = CreateMocks();
        sm.Setup(x => x.PerformPasskeyAttestationAsync("valid-cred"))
            .ReturnsAsync(successfulAttestation);
        um.Setup(x => x.AddOrUpdatePasskeyAsync(user, passkey))
            .ReturnsAsync(IdentityResult.Success);

        var service = new PasskeyService(um.Object, sm.Object, ts.Object);
        var result = await service.CompleteRegistrationAsync(user, "valid-cred");

        Assert.True(result.IsSuccess);
        um.Verify(x => x.AddOrUpdatePasskeyAsync(user, passkey), Times.Once);
    }
}
