using Identity.Api.Domain.Entities;
using Identity.Api.Infrastructure.Persistence;
using Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Resend;
using Utilities;

namespace Identity.Api.Application.Services;

public record AuthTokenDto(string AccessToken, double ExpiresIn, string RefreshToken);

public interface IAuthService
{
    Task<Result<AuthTokenDto>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<Result> RegisterAsync(string email, string password,
        CancellationToken cancellationToken = default);

    Task<Result<AuthTokenDto>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task RevokeOtherSessionsAsync(string refreshToken, string userId, CancellationToken cancellationToken = default);

    Task<Result<AuthTokenDto>> ConfirmEmail(string userId, string emailToken,
        CancellationToken cancellationToken = default);

    Task<Result> ResendConfirmationEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Result> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);
    Task<Result> ResetPasswordAsync(string userId, string token, string password, CancellationToken cancellationToken = default);
}

public class AuthService(
    UserManager<ApplicationUser> userManager,
    IdentityAppDbContext dbContext,
    IConfiguration configuration,
    ILogger<AuthService> logger,
    IDateTimeProvider dateTimeProvider,
    IResend emailService) : IAuthService
{
    private readonly string _jwtSecret = configuration["Jwt:Secret"]
                                         ?? throw new InvalidOperationException("Jwt:Secret is not configured");

    private readonly int _accessTokenMinutes = configuration.GetValue("Jwt:ExpiresInMinutes", 60);
    private readonly int _refreshTokenDays = configuration.GetValue("Jwt:RefreshTokenDays", 30);

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
        return await BuildTokenAsync(user, cancellationToken);
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

        var emailToken = await userManager
            .GenerateEmailConfirmationTokenAsync(user);
        var encodedEmailToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(emailToken));
        var frontUrl = configuration["FrontendUrl"]
            ?? throw new InvalidOperationException("FrontendUrl is not configured");
        if (!frontUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !frontUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("FrontendUrl must include the protocol (https://)");
        var confirmationLink =
            $"{frontUrl.TrimEnd('/')}/confirm-email" +
            $"?userId={user.Id}" +
            $"&token={encodedEmailToken}";

        var message = new EmailMessage
        {
            From = "MCollector <noreply@mail.mcollector.publicvm.com>",
            Subject = "Подтвердите email",
            HtmlBody = BuildConfirmationEmailHtml(confirmationLink, frontUrl, dateTimeProvider.UtcNow.Year)
        };
        message.To.Add(email);

        try
        {
            await emailService.EmailSendAsync(message, cancellationToken);
        }
        catch (Exception e)
        {
            if (logger.IsEnabled(LogLevel.Error))
                logger.LogError(e, "Failed to send confirmation email to {Email}", email);
            return Errors.Internal("Failed to send confirmation email");
        }

        return Result.Success();
    }

    public async Task<Result<AuthTokenDto>> RefreshAsync(string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var token = await dbContext.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.Token == refreshToken, cancellationToken);

        if (token is null || !token.IsActive(dateTimeProvider.UtcNow))
            return Errors.Unauthorized("Invalid or expired refresh token");

        token.RevokedAt = dateTimeProvider.UtcNow;
        return await BuildTokenAsync(token.User, cancellationToken);
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

        var decodedTokenBytes = WebEncoders.Base64UrlDecode(emailToken);
        var decodedToken = Encoding.UTF8.GetString(decodedTokenBytes);
        var result = await userManager.ConfirmEmailAsync(user, decodedToken);

        return result.Succeeded
            ? await BuildTokenAsync(user, cancellationToken)
            : Errors.Validation("Email Confirmation", string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    public async Task<Result> ResendConfirmationEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null || user.EmailConfirmed)
            return Result.Success();

        var emailToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedEmailToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(emailToken));
        var frontUrl = configuration["FrontendUrl"]
            ?? throw new InvalidOperationException("FrontendUrl is not configured");
        if (!frontUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !frontUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("FrontendUrl must include the protocol (https://)");
        var confirmationLink =
            $"{frontUrl.TrimEnd('/')}/confirm-email" +
            $"?userId={user.Id}" +
            $"&token={encodedEmailToken}";

        var message = new EmailMessage
        {
            From = "MCollector <noreply@mail.mcollector.publicvm.com>",
            Subject = "Подтвердите email",
            HtmlBody = BuildConfirmationEmailHtml(confirmationLink, frontUrl, dateTimeProvider.UtcNow.Year)
        };
        message.To.Add(email);

        try
        {
            await emailService.EmailSendAsync(message, cancellationToken);
        }
        catch (Exception e)
        {
            if (logger.IsEnabled(LogLevel.Error))
                logger.LogError(e, "Failed to resend confirmation email to {Email}", email);
            return Errors.Internal("Failed to send confirmation email");
        }

        return Result.Success();
    }

    public async Task<Result> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null || !user.EmailConfirmed)
            return Result.Success();

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(resetToken));
        var frontUrl = configuration["FrontendUrl"]
            ?? throw new InvalidOperationException("FrontendUrl is not configured");
        if (!frontUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !frontUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("FrontendUrl must include the protocol (https://)");
        var resetLink =
            $"{frontUrl.TrimEnd('/')}/reset-password" +
            $"?userId={user.Id}" +
            $"&token={encodedToken}";

        var message = new EmailMessage
        {
            From = "MCollector <noreply@mail.mcollector.publicvm.com>",
            Subject = "Сброс пароля",
            HtmlBody = BuildPasswordResetEmailHtml(resetLink, frontUrl, dateTimeProvider.UtcNow.Year)
        };
        message.To.Add(email);

        try
        {
            await emailService.EmailSendAsync(message, cancellationToken);
        }
        catch (Exception e)
        {
            if (logger.IsEnabled(LogLevel.Error))
                logger.LogError(e, "Failed to send password reset email to {Email}", email);
            return Errors.Internal("Failed to send password reset email");
        }

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(string userId, string token, string password,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return Errors.Validation("ResetPassword", "Invalid reset link");

        var decodedTokenBytes = WebEncoders.Base64UrlDecode(token);
        var decodedToken = Encoding.UTF8.GetString(decodedTokenBytes);
        var result = await userManager.ResetPasswordAsync(user, decodedToken, password);

        if (!result.Succeeded)
            return Errors.Validation("ResetPassword", string.Join("; ", result.Errors.Select(e => e.Description)));

        await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, dateTimeProvider.UtcNow), cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("User {UserId} reset their password", userId);

        return Result.Success();
    }

    private static string BuildPasswordResetEmailHtml(string resetLink, string frontUrl, int year)
    {
        var safeLink = resetLink.Replace("&", "&amp;");
        return $"""
            <div style="font-family:Arial,sans-serif;max-width:480px;margin:0 auto;color:#1a1a1a">
              <p>Вы запросили сброс пароля. Нажмите на кнопку ниже, чтобы задать новый пароль:</p>
              <p><a href="{safeLink}" style="display:inline-block;padding:10px 20px;background:#18181b;color:#fff;text-decoration:none;border-radius:6px">Сбросить пароль</a></p>
              <p style="color:#888;font-size:12px;margin-top:24px">Если вы не запрашивали сброс пароля, просто проигнорируйте это письмо.</p>
              <p style="color:#888;font-size:12px">Если кнопка не работает, скопируйте ссылку в браузер:</p>
              <p style="color:#888;font-size:12px;word-break:break-all">{resetLink}</p>
              <hr style="border:none;border-top:1px solid #eee;margin:24px 0">
              <p style="color:#aaa;font-size:11px;text-align:center;margin:0">
                © {year} MCollector &nbsp;·&nbsp;
                <a href="{frontUrl}" style="color:#aaa;text-decoration:none">{frontUrl}</a>
              </p>
            </div>
            """;
    }

    private static string BuildConfirmationEmailHtml(string confirmationLink, string frontUrl, int year)
    {
        var safeLink = confirmationLink.Replace("&", "&amp;");
        return $"""
            <div style="font-family:Arial,sans-serif;max-width:480px;margin:0 auto;color:#1a1a1a">
              <p>Спасибо за регистрацию! Нажмите на кнопку ниже, чтобы подтвердить ваш email:</p>
              <p><a href="{safeLink}" style="display:inline-block;padding:10px 20px;background:#18181b;color:#fff;text-decoration:none;border-radius:6px">Подтвердить email</a></p>
              <p style="color:#888;font-size:12px;margin-top:24px">Если кнопка не работает, скопируйте ссылку в браузер:</p>
              <p style="color:#888;font-size:12px;word-break:break-all">{confirmationLink}</p>
              <hr style="border:none;border-top:1px solid #eee;margin:24px 0">
              <p style="color:#aaa;font-size:11px;text-align:center;margin:0">
                © {year} MCollector &nbsp;·&nbsp;
                <a href="{frontUrl}" style="color:#aaa;text-decoration:none">{frontUrl}</a>
              </p>
            </div>
            """;
    }

    private async Task<AuthTokenDto> BuildTokenAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        await dbContext.RefreshTokens
            .Where(t => t.UserId == user.Id && (t.RevokedAt != null || t.ExpiresAt <= now))
            .ExecuteDeleteAsync(cancellationToken);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
        };

        var jwtToken = new JwtSecurityToken(
            claims: claims,
            expires: now.AddMinutes(_accessTokenMinutes),
            signingCredentials: new(key, SecurityAlgorithms.HmacSha256),
            issuer: SharedAuthExtensions.Issuer,
            audience: SharedAuthExtensions.Audience
        );

        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        var refreshTokenValue = Convert.ToBase64String(bytes);

        dbContext.RefreshTokens.Add(new()
        {
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiresAt = now.AddDays(_refreshTokenDays),
            CreatedAt = now,
            User = user
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new(
            new JwtSecurityTokenHandler().WriteToken(jwtToken),
            TimeSpan.FromMinutes(_accessTokenMinutes).TotalSeconds,
            refreshTokenValue
        );
    }
}
