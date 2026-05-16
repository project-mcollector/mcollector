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
        um.Setup(x => x.GetPasskeysAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync([]);
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

    // ExtractAaguid

    [Fact]
    public void ExtractAaguid_ValidAttestationObject_ReturnsAaguidString()
    {
        // Build a minimal CBOR attestation object:
        //   A2 ("fmt" → "none", "authData" → bytes)
        // authData: 32-byte rpIdHash | flags=0x45 (UP+UV+AT) | signCount(4) | aaguid(16) | ...
        var rpIdHash = new byte[32]; // all zeros for test
        byte flags = 0x45; // UP | UV | AT set
        var signCount = new byte[4];
        // Use Apple iCloud Keychain AAGUID: fbfc3007-154e-4ecc-8c0b-6e020557d7bd
        var aaguid = new byte[] { 0xfb, 0xfc, 0x30, 0x07, 0x15, 0x4e, 0x4e, 0xcc, 0x8c, 0x0b, 0x6e, 0x02, 0x05, 0x57, 0xd7, 0xbd };
        // Minimal credential data suffix (credId length = 0)
        var credIdLen = new byte[] { 0x00, 0x01 };
        var credId = new byte[] { 0xAB };
        var authData = rpIdHash.Concat(new[] { flags }).Concat(signCount).Concat(aaguid).Concat(credIdLen).Concat(credId).ToArray();

        // CBOR: A2 (map 2 entries)
        //   63 "fmt" → 64 "none"
        //   68 "authData" → 5A <4-byte len> <authData bytes>
        var fmtKey = new byte[] { 0x63, 0x66, 0x6D, 0x74 };               // text(3) "fmt"
        var fmtVal = new byte[] { 0x64, 0x6E, 0x6F, 0x6E, 0x65 };         // text(4) "none"
        var adKey = new byte[] { 0x68, 0x61, 0x75, 0x74, 0x68, 0x44, 0x61, 0x74, 0x61 }; // text(8) "authData"
        // bytes(len): major type 2, 4-byte length (info=26)
        int adLen = authData.Length;
        var adLenBytes = new byte[] { (byte)(adLen >> 24), (byte)(adLen >> 16), (byte)(adLen >> 8), (byte)adLen };
        var adHeader = new byte[] { 0x5A }.Concat(adLenBytes).ToArray();   // bytes(26 → 4-byte len)

        var attestationObject = new byte[] { 0xA2 }
            .Concat(fmtKey).Concat(fmtVal)
            .Concat(adKey).Concat(adHeader).Concat(authData)
            .ToArray();

        var result = PasskeyService.ExtractAaguid(attestationObject);

        Assert.Equal("fbfc3007-154e-4ecc-8c0b-6e020557d7bd", result);
    }

    [Fact]
    public void ExtractAaguid_InvalidBytes_ReturnsNull()
    {
        Assert.Null(PasskeyService.ExtractAaguid([10, 11, 12]));
    }

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

    [Fact]
    public async Task BeginRegistrationAsync_BelowLimit_AllowsCreatingOnAnotherDevice()
    {
        var user = CreateUser();
        var existing = CreateDummyPasskeyInfo();
        const string optionsJson = "{\"challenge\":\"reg\"}";
        var (um, sm, ts) = CreateMocks();
        um.Setup(x => x.GetPasskeysAsync(user))
            .ReturnsAsync([existing]);
        sm.Setup(x => x.MakePasskeyCreationOptionsAsync(It.IsAny<PasskeyUserEntity>()))
            .ReturnsAsync(optionsJson);

        var service = new PasskeyService(um.Object, sm.Object, ts.Object);
        var result = await service.BeginRegistrationAsync(user);

        Assert.True(result.IsSuccess);
        Assert.Equal(optionsJson, result.Value);
        sm.Verify(x => x.MakePasskeyCreationOptionsAsync(It.IsAny<PasskeyUserEntity>()), Times.Once);
    }

    [Fact]
    public async Task BeginRegistrationAsync_AtLimit_ReturnsConflict()
    {
        var user = CreateUser();
        var existing = Enumerable.Range(0, 5).Select(_ => CreateDummyPasskeyInfo()).ToList();
        var (um, sm, ts) = CreateMocks();
        um.Setup(x => x.GetPasskeysAsync(user))
            .ReturnsAsync(existing);

        var service = new PasskeyService(um.Object, sm.Object, ts.Object);
        var result = await service.BeginRegistrationAsync(user);

        Assert.False(result.IsSuccess);
        Assert.Equal("Conflict", result.Error?.Id);
        sm.Verify(x => x.MakePasskeyCreationOptionsAsync(It.IsAny<PasskeyUserEntity>()), Times.Never);
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

    [Fact]
    public async Task CompleteRegistrationAsync_BelowLimit_AllowsCreatingOnAnotherDevice()
    {
        var user = CreateUser();
        var passkey = CreateDummyPasskeyInfo();
        var existing = CreateDummyPasskeyInfo();
        var userEntity = new PasskeyUserEntity { Id = user.Id, Name = user.UserName, DisplayName = user.Email };
        var successfulAttestation = PasskeyAttestationResult.Success(passkey, userEntity);
        var (um, sm, ts) = CreateMocks();
        um.Setup(x => x.GetPasskeysAsync(user))
            .ReturnsAsync([existing]);
        sm.Setup(x => x.PerformPasskeyAttestationAsync("valid-cred"))
            .ReturnsAsync(successfulAttestation);
        um.Setup(x => x.AddOrUpdatePasskeyAsync(user, passkey))
            .ReturnsAsync(IdentityResult.Success);

        var service = new PasskeyService(um.Object, sm.Object, ts.Object);
        var result = await service.CompleteRegistrationAsync(user, "valid-cred");

        Assert.True(result.IsSuccess);
        um.Verify(x => x.AddOrUpdatePasskeyAsync(user, passkey), Times.Once);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_AtLimit_ReturnsConflict()
    {
        var user = CreateUser();
        var existing = Enumerable.Range(0, 5).Select(_ => CreateDummyPasskeyInfo()).ToList();
        var (um, sm, ts) = CreateMocks();
        um.Setup(x => x.GetPasskeysAsync(user))
            .ReturnsAsync(existing);

        var service = new PasskeyService(um.Object, sm.Object, ts.Object);
        var result = await service.CompleteRegistrationAsync(user, "valid-cred");

        Assert.False(result.IsSuccess);
        Assert.Equal("Conflict", result.Error?.Id);
        sm.Verify(x => x.PerformPasskeyAttestationAsync(It.IsAny<string>()), Times.Never);
        um.Verify(x => x.AddOrUpdatePasskeyAsync(It.IsAny<ApplicationUser>(), It.IsAny<UserPasskeyInfo>()), Times.Never);
    }

    // DeleteAsync

    [Fact]
    public async Task DeleteAsync_InvalidBase64_ReturnsValidation()
    {
        var user = CreateUser();
        var (um, sm, ts) = CreateMocks();

        var service = new PasskeyService(um.Object, sm.Object, ts.Object);
        var result = await service.DeleteAsync(user, "not-valid-base64!!!");

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation.Passkey", result.Error?.Id);
        um.Verify(x => x.RemovePasskeyAsync(It.IsAny<ApplicationUser>(), It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_PasskeyNotOwnedByUser_ReturnsNotFound()
    {
        var user = CreateUser();
        var (um, sm, ts) = CreateMocks();
        // default mock returns empty list

        var service = new PasskeyService(um.Object, sm.Object, ts.Object);
        var result = await service.DeleteAsync(user, "AQID");

        Assert.False(result.IsSuccess);
        Assert.Equal("Passkey.NotFound", result.Error?.Id);
        um.Verify(x => x.RemovePasskeyAsync(It.IsAny<ApplicationUser>(), It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_OwnedPasskey_CallsRemoveAndReturnsSuccess()
    {
        var user = CreateUser();
        var passkey = CreateDummyPasskeyInfo(); // credentialId = [1,2,3] → "AQID"
        var (um, sm, ts) = CreateMocks();
        um.Setup(x => x.GetPasskeysAsync(user)).ReturnsAsync([passkey]);
        um.Setup(x => x.RemovePasskeyAsync(user, It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 1, 2, 3 }))))
            .ReturnsAsync(IdentityResult.Success);

        var service = new PasskeyService(um.Object, sm.Object, ts.Object);
        var result = await service.DeleteAsync(user, "AQID");

        Assert.True(result.IsSuccess);
        um.Verify(x => x.RemovePasskeyAsync(user, It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 1, 2, 3 }))), Times.Once);
    }

    [Fact]
    public async Task ListAsync_ReturnsMappedPasskeys()
    {
        var user = CreateUser();
        var passkey = CreateDummyPasskeyInfo();
        var (um, sm, ts) = CreateMocks();
        um.Setup(x => x.GetPasskeysAsync(user))
            .ReturnsAsync([passkey]);

        var service = new PasskeyService(um.Object, sm.Object, ts.Object);
        var result = await service.ListAsync(user);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("AQID", result.Value[0].CredentialId);
        Assert.Equal(passkey.CreatedAt, result.Value[0].CreatedAt);
        Assert.Equal(passkey.Transports, result.Value[0].Transports);
        Assert.Equal(passkey.IsBackupEligible, result.Value[0].IsBackupEligible);
        Assert.Null(result.Value[0].Aaguid); // dummy attestation bytes don't parse
    }
}
