using System.Text;
using Identity.Api.Domain.Entities;
using Identity.Api.Infrastructure.Telemetry;
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
    bool IsBackupEligible,
    string? Aaguid);

public sealed class PasskeyService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ITokenService tokenService,
    ILogger<PasskeyService>? logger = null,
    IdentityMetrics? metrics = null)
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
                p.IsBackupEligible,
                ExtractAaguid(p.AttestationObject)))
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
        {
            metrics?.RecordPasskeyRegistration(IdentityMetrics.Outcomes.LimitReached);
            return Errors.Conflict($"Passkey limit reached ({MaxPasskeysPerUser}) for this account");
        }

        var attestation = await signInManager.PerformPasskeyAttestationAsync(credentialJson);
        if (!attestation.Succeeded)
        {
            logger?.LogWarning(
                "Passkey attestation failed for user {UserId}: {Failure}",
                user.Id,
                attestation.Failure?.Message ?? "unknown failure");
            metrics?.RecordPasskeyRegistration(IdentityMetrics.Outcomes.AttestationFailed);
            return Errors.Validation("Passkey", attestation.Failure?.Message ?? "Registration failed");
        }
        await userManager.AddOrUpdatePasskeyAsync(user, attestation.Passkey);
        metrics?.RecordPasskeyRegistration(IdentityMetrics.Outcomes.Success);
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
            metrics?.RecordPasskeyLogin(IdentityMetrics.Outcomes.AssertionFailed);
            return Errors.Unauthorized("passkey");
        }

        metrics?.RecordPasskeyLogin(IdentityMetrics.Outcomes.Success);
        return await tokenService.CreateTokenAsync(result.User, cancellationToken);
    }

    private async Task<bool> HasReachedPasskeyLimitAsync(ApplicationUser user)
    {
        var passkeys = await userManager.GetPasskeysAsync(user);
        return passkeys.Count >= MaxPasskeysPerUser;
    }

    // Extracts the AAGUID from a WebAuthn attestation object (CBOR-encoded).
    // The attestation object is a map: { "fmt": text, "attStmt": map, "authData": bytes }.
    // authData layout: rpIdHash(32) | flags(1) | signCount(4) | aaguid(16) | ...
    public static string? ExtractAaguid(byte[] attestationObject)
    {
        try
        {
            int pos = 0;
            if (!TryReadCborMapHeader(attestationObject, ref pos, out int itemCount)) return null;

            for (int i = 0; i < itemCount; i++)
            {
                string? key = TryReadCborTextString(attestationObject, ref pos);
                if (key is null) return null;

                if (key == "authData")
                {
                    byte[]? authData = TryReadCborByteString(attestationObject, ref pos);
                    if (authData is null || authData.Length < 53) return null;
                    if ((authData[32] & 0x40) == 0) return null; // AT flag not set

                    var aaguid = new Guid(authData.AsSpan()[37..53], bigEndian: true);
                    return aaguid == Guid.Empty ? null : aaguid.ToString();
                }

                SkipCborValue(attestationObject, ref pos);
            }
            return null;
        }
        catch { return null; }
    }

    private static bool TryReadCborMapHeader(byte[] d, ref int pos, out int count)
    {
        count = 0;
        if (pos >= d.Length) return false;
        byte b = d[pos++];
        return (b >> 5) == 5 && TryReadCborLength(d, ref pos, b & 0x1F, out count);
    }

    private static string? TryReadCborTextString(byte[] d, ref int pos)
    {
        if (pos >= d.Length) return null;
        byte b = d[pos++];
        if ((b >> 5) != 3) return null;
        if (!TryReadCborLength(d, ref pos, b & 0x1F, out int len) || pos + len > d.Length) return null;
        var s = Encoding.UTF8.GetString(d, pos, len);
        pos += len;
        return s;
    }

    private static byte[]? TryReadCborByteString(byte[] d, ref int pos)
    {
        if (pos >= d.Length) return null;
        byte b = d[pos++];
        if ((b >> 5) != 2) return null;
        if (!TryReadCborLength(d, ref pos, b & 0x1F, out int len) || pos + len > d.Length) return null;
        var bytes = d[pos..(pos + len)];
        pos += len;
        return bytes;
    }

    private static bool TryReadCborLength(byte[] d, ref int pos, int info, out int length)
    {
        if (info < 24) { length = info; return true; }
        if (info == 24) { if (pos >= d.Length) { length = 0; return false; } length = d[pos++]; return true; }
        if (info == 25) { if (pos + 2 > d.Length) { length = 0; return false; } length = (d[pos] << 8) | d[pos + 1]; pos += 2; return true; }
        if (info == 26) { if (pos + 4 > d.Length) { length = 0; return false; } length = (d[pos] << 24) | (d[pos + 1] << 16) | (d[pos + 2] << 8) | d[pos + 3]; pos += 4; return true; }
        length = 0; return false;
    }

    private static void SkipCborValue(byte[] d, ref int pos)
    {
        if (pos >= d.Length) return;
        byte b = d[pos++];
        int mt = b >> 5, info = b & 0x1F;
        switch (mt)
        {
            case 0: case 1: TryReadCborLength(d, ref pos, info, out _); break;
            case 2:
            case 3:
                if (TryReadCborLength(d, ref pos, info, out int sLen)) pos += sLen;
                break;
            case 4:
                if (TryReadCborLength(d, ref pos, info, out int aLen))
                    for (int i = 0; i < aLen; i++) SkipCborValue(d, ref pos);
                break;
            case 5:
                if (TryReadCborLength(d, ref pos, info, out int mLen))
                    for (int i = 0; i < mLen * 2; i++) SkipCborValue(d, ref pos);
                break;
            case 6: SkipCborValue(d, ref pos); break;
            case 7:
                if (info == 25) pos += 2;
                else if (info == 26) pos += 4;
                else if (info == 27) pos += 8;
                break;
        }
    }
}
