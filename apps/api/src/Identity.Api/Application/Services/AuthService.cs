using Identity.Api.Domain.Entities;
using Identity.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Utilities;

namespace Identity.Api.Application.Services;

public record AuthTokenDto(string AccessToken, double ExpiresIn, string RefreshToken);

public interface IAuthService
{
    Task<Result<AuthTokenDto>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<Result> RegisterAsync(string email, string password,
        CancellationToken cancellationToken = default);

    Task<Result<string>> BeginPasskeyRegistrationAsync(ApplicationUser user);
    Task<Result> CompletePasskeyRegistrationAsync(ApplicationUser user, string credentialJson);
    Task<Result<string>> BeginPasskeyLoginAsync(string email);
    Task<Result<string>> BeginDiscoverablePasskeyLoginAsync();
    Task<Result<AuthTokenDto>> CompletePasskeyLoginAsync(string credentialJson);

    Task<Result<AuthTokenDto>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task RevokeOtherSessionsAsync(string refreshToken, string userId, CancellationToken cancellationToken = default);

    Task<Result<AuthTokenDto>> ConfirmEmail(string userId, string emailToken,
        CancellationToken cancellationToken = default);

    Task<Result> ResendConfirmationEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Result> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);

    Task<Result> ResetPasswordAsync(string userId, string token, string password,
        CancellationToken cancellationToken = default);
}

public class AuthService(
    UserManager<ApplicationUser> userManager,
    IdentityAppDbContext dbContext,
    ILogger<AuthService> logger,
    IDateTimeProvider dateTimeProvider,
    IPasskeyService passkeyService,
    ITokenService tokenService,
    IMailService mailService) : IAuthService
{
    public async Task<Result<AuthTokenDto>> LoginAsync(string email, string password,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Failed login attempt for {Email}", email);
            return Errors.Unauthorized(email);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Locked-out login attempt for {UserId}", user.Id);
            return Errors.Unauthorized(email);
        }

        if (!await userManager.IsEmailConfirmedAsync(user))
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Unconfirmed email login attempt for {UserId}", user.Id);
            return Errors.EmailNotConfirmed();
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Failed login attempt for {UserId}", user.Id);
            return Errors.Unauthorized(email);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("User {UserId} logged in", user.Id);
        return await tokenService.CreateTokenAsync(user, cancellationToken);
    }

    public async Task<Result> RegisterAsync(string email, string password,
        CancellationToken cancellationToken = default)
    {
        var user = new ApplicationUser { Email = email, UserName = email };
        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Registration failed for {Email}: {Errors}", email, errors);
            return Errors.Validation("Registration", errors);
        }

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("User {UserId} registered with email {Email}", user.Id, email);

        return await mailService.SendConfirmationEmailAsync(user, cancellationToken);
    }

    public async Task<Result<string>> BeginPasskeyRegistrationAsync(ApplicationUser user)
        => await passkeyService.BeginRegistrationAsync(user);

    public async Task<Result> CompletePasskeyRegistrationAsync(ApplicationUser user, string credentialJson)
        => await passkeyService.CompleteRegistrationAsync(user, credentialJson);

    public async Task<Result<string>> BeginPasskeyLoginAsync(string email)
        => await passkeyService.BeginLoginAsync(email);

    public async Task<Result<string>> BeginDiscoverablePasskeyLoginAsync()
        => await passkeyService.BeginDiscoverableLoginAsync();

    public async Task<Result<AuthTokenDto>> CompletePasskeyLoginAsync(string credentialJson)
        => await passkeyService.CompleteLoginAsync(credentialJson);

    public async Task<Result<AuthTokenDto>> RefreshAsync(string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;

        var token = await dbContext.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.Token == refreshToken, cancellationToken);

        if (token is null || token.IsExpired(now))
            return Errors.Unauthorized("Invalid or expired refresh token");

        var revoked = await dbContext.RefreshTokens
            .Where(t => t.Token == refreshToken && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);

        if (revoked != 0)
            return await tokenService.CreateTokenAsync(token.User, cancellationToken);

        await dbContext.RefreshTokens
            .Where(t => t.UserId == token.UserId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);

        if (logger.IsEnabled(LogLevel.Warning))
            logger.LogWarning("Refresh token reuse detected for user {UserId} — all sessions revoked",
                token.UserId);

        return Errors.Unauthorized("Invalid or expired refresh token");
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var token = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(t => t.Token == refreshToken, cancellationToken);

        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = dateTimeProvider.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("User {UserId} revoked refresh token", token.UserId);
        }
    }

    public async Task RevokeOtherSessionsAsync(string refreshToken, string userId,
        CancellationToken cancellationToken = default)
    {
        await dbContext.RefreshTokens
            .Where(t => t.Token != refreshToken && t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, dateTimeProvider.UtcNow), cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("User {UserId} revoked other refresh tokens", userId);
    }

    public async Task<Result<AuthTokenDto>> ConfirmEmail(string userId, string emailToken,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return Errors.Unauthorized("Invalid user id");

        var result = await mailService.ConfirmEmail(user, emailToken, cancellationToken);

        return result.IsSuccess
            ? await tokenService.CreateTokenAsync(user, cancellationToken)
            : Errors.Validation("Email Confirmation", result.Error.Description);
    }

    public async Task<Result> ResendConfirmationEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null || user.EmailConfirmed)
            return Result.Success();

        return await mailService.SendConfirmationEmailAsync(user, cancellationToken);
    }

    public async Task<Result> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null || !user.EmailConfirmed)
            return Result.Success();

        return await mailService.SendPasswordResetEmailAsync(user, cancellationToken);
    }

    public async Task<Result> ResetPasswordAsync(string userId, string token, string password,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return Errors.Validation("ResetPassword", "Invalid reset link");

        var decodedToken = DecodeToken(token);
        if (decodedToken.IsFailure)
            return Errors.Validation("ResetPassword", decodedToken.Error.Description);

        var result = await userManager.ResetPasswordAsync(user, decodedToken.Value, password);

        if (!result.Succeeded)
            return Errors.Validation("ResetPassword", string.Join("; ", result.Errors.Select(e => e.Description)));

        await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, dateTimeProvider.UtcNow), cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("User {UserId} reset their password", userId);

        return Result.Success();
    }

    private static Result<string> DecodeToken(string encodedToken)
    {
        try
        {
            var bytes = WebEncoders.Base64UrlDecode(encodedToken);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            return Errors.Validation("EmailToken", "Invalid reset link");
        }
    }
}
