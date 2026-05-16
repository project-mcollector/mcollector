using Identity.Api.Domain.Entities;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Identity;
using Utilities;

namespace Identity.Api.Application.Services;

public interface IPasskeyService
{
    Task<Result<IReadOnlyList<PasskeyDto>>> ListAsync(ApplicationUser user,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(ApplicationUser user, string credentialId,
        CancellationToken cancellationToken = default);

    Task<Result<string>> BeginRegistrationAsync(ApplicationUser user,
        CancellationToken cancellationToken = default);

    Task<Result> CompleteRegistrationAsync(ApplicationUser user, string credentialJson,
        CancellationToken cancellationToken = default);

    Task<Result<string>> BeginLoginAsync(string email,
        CancellationToken cancellationToken = default);

    Task<Result<string>> BeginDiscoverableLoginAsync(
        CancellationToken cancellationToken = default);

    Task<Result<AuthTokenDto>> CompleteLoginAsync(string credentialJson,
        CancellationToken cancellationToken = default);
}

public sealed record PasskeyDto(
    string CredentialId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> Transports,
    bool IsBackupEligible);

public sealed class PasskeyService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ITokenService tokenService,
    ILogger<PasskeyService>? logger = null)
    : IPasskeyService
{
    private const int MaxPasskeysPerUser = 5;

    public async Task<Result<IReadOnlyList<PasskeyDto>>> ListAsync(ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var passkeys = await userManager.GetPasskeysAsync(user);
        var list = passkeys
            .Select(p => new PasskeyDto(
                WebEncoders.Base64UrlEncode(p.CredentialId),
                p.CreatedAt,
                p.Transports,
                p.IsBackupEligible))
            .ToList();
        return list;
    }

    public async Task<Result> DeleteAsync(ApplicationUser user, string credentialId,
        CancellationToken cancellationToken = default)
    {
        byte[] credentialIdBytes;
        try
        {
            credentialIdBytes = WebEncoders.Base64UrlDecode(credentialId);
        }
        catch (FormatException)
        {
            return Errors.Validation("Passkey", "Invalid credential ID");
        }

        var passkeys = await userManager.GetPasskeysAsync(user);
        if (!passkeys.Any(p => p.CredentialId.SequenceEqual(credentialIdBytes)))
            return Errors.NotFound("Passkey", credentialId);

        await userManager.RemovePasskeyAsync(user, credentialIdBytes);
        return Result.Success();
    }

    public async Task<Result<string>> BeginRegistrationAsync(ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        if (await HasReachedPasskeyLimitAsync(user))
            return Errors.Conflict($"Passkey limit reached ({MaxPasskeysPerUser}) for this account");

        var userEntity = new PasskeyUserEntity
        {
            Id = user.Id,
            Name = user.UserName ?? user.Email ?? user.Id,
            DisplayName = user.Email ?? user.UserName ?? user.Id
        };

        return await signInManager.MakePasskeyCreationOptionsAsync(userEntity);
    }

    public async Task<Result> CompleteRegistrationAsync(ApplicationUser user, string credentialJson,
        CancellationToken cancellationToken = default)
    {
        if (await HasReachedPasskeyLimitAsync(user))
            return Errors.Conflict($"Passkey limit reached ({MaxPasskeysPerUser}) for this account");

        var attestation = await signInManager.PerformPasskeyAttestationAsync(credentialJson);
        if (!attestation.Succeeded)
        {
            logger?.LogWarning(
                "Passkey attestation failed for user {UserId}: {Failure}",
                user.Id,
                attestation.Failure?.Message ?? "unknown failure");
            return Errors.Validation("Passkey", attestation.Failure?.Message ?? "Registration failed");
        }
        await userManager.AddOrUpdatePasskeyAsync(user, attestation.Passkey);
        return Result.Success();
    }

    public async Task<Result<string>> BeginLoginAsync(string email,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
            return Errors.NotFound("User", email);

        return await signInManager.MakePasskeyRequestOptionsAsync(user);
    }

    public async Task<Result<string>> BeginDiscoverableLoginAsync(
        CancellationToken cancellationToken = default)
        => await signInManager.MakePasskeyRequestOptionsAsync(null);

    public async Task<Result<AuthTokenDto>> CompleteLoginAsync(string credentialJson,
        CancellationToken cancellationToken = default)
    {
        var result = await signInManager.PerformPasskeyAssertionAsync(credentialJson);
        if (!result.Succeeded)
        {
            logger?.LogWarning("Passkey assertion failed: {Failure}", result.Failure?.Message ?? "unknown failure");
            return Errors.Unauthorized("passkey");
        }

        return await tokenService.CreateTokenAsync(result.User, cancellationToken);
    }

    private async Task<bool> HasReachedPasskeyLimitAsync(ApplicationUser user)
    {
        var passkeys = await userManager.GetPasskeysAsync(user);
        return passkeys.Count >= MaxPasskeysPerUser;
    }
}
