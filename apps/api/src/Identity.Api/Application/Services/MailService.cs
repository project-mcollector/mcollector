using System.Text;
using Identity.Api.Application.Options;
using Identity.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Resend;
using Utilities;

namespace Identity.Api.Application.Services;

public interface IMailService
{
    Task<Result> SendEmailAsync(string to, string subject, string html,
        CancellationToken cancellationToken = default);

    Task<Result> ConfirmEmail(ApplicationUser user, string emailToken,
        CancellationToken cancellationToken = default);

    Task<Result> SendConfirmationEmailAsync(ApplicationUser user,
        CancellationToken cancellationToken = default);

    Task<Result> SendPasswordResetEmailAsync(ApplicationUser user,
        CancellationToken cancellationToken = default);
}

public class MailService(
    UserManager<ApplicationUser> userManager,
    IResend emailService,
    IOptions<MailOptions> mailOptions,
    IDateTimeProvider dateTimeProvider,
    ILogger<MailService> logger)
    : IMailService
{
    private readonly string _frontendUrl = mailOptions.Value.FrontendUrl;
    private readonly string _fromAddress = mailOptions.Value.FromAddress;

    public async Task<Result> SendEmailAsync(string to, string subject, string html,
        CancellationToken cancellationToken = default)
    {
        var message = new EmailMessage { From = _fromAddress, Subject = subject, HtmlBody = html };
        message.To.Add(to);

        try
        {
            await emailService.EmailSendAsync(message, cancellationToken);
            return Result.Success();
        }
        catch (Exception e)
        {
            if (logger.IsEnabled(LogLevel.Error))
                logger.LogError(e, "Failed to send email to {To}", to);
            return Errors.Internal("Failed to send email");
        }
    }

    public async Task<Result> ConfirmEmail(ApplicationUser user, string emailToken,
        CancellationToken cancellationToken = default)
    {
        string decodedToken;
        try
        {
            decodedToken = DecodeBase64UrlToken(emailToken);
        }
        catch (FormatException)
        {
            return Errors.Validation("Email Confirmation", "Invalid token");
        }

        var result = await userManager.ConfirmEmailAsync(user, decodedToken);
        return result.Succeeded
            ? Result.Success()
            : Errors.Validation("Email Confirmation", string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    public async Task<Result> SendConfirmationEmailAsync(ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var emailToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(emailToken));
        var frontUrl = _frontendUrl;
        var link = $"{frontUrl.TrimEnd('/')}/confirm-email?userId={user.Id}&token={encodedToken}";

        if (user.Email is null)
            return Errors.Validation("Email Confirmation", "User does not have an email");

        return await SendEmailAsync(user.Email, "Подтвердите email",
            BuildConfirmationEmailHtml(link, frontUrl, dateTimeProvider.UtcNow.Year),
            cancellationToken);
    }

    public async Task<Result> SendPasswordResetEmailAsync(ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(resetToken));
        var frontUrl = _frontendUrl;
        var link = $"{frontUrl.TrimEnd('/')}/reset-password?userId={user.Id}&token={encodedToken}";

        if (user.Email is null)
            return Errors.Validation("Email Confirmation", "User does not have an email");

        return await SendEmailAsync(user.Email, "Сброс пароля",
            BuildPasswordResetEmailHtml(link, frontUrl, dateTimeProvider.UtcNow.Year),
            cancellationToken);
    }

    private static string BuildConfirmationEmailHtml(string confirmationLink, string frontUrl, int year)
    {
        var safeLink = confirmationLink.Replace("&", "&amp;");
        return $"""
                <div style="font-family:Arial,sans-serif;max-width:480px;margin:0 auto;color:#1a1a1a">
                  <p>Спасибо за регистрацию! Нажмите на кнопку ниже, чтобы подтвердить ваш email:</p>
                  <p><a href="{safeLink}" style="display:inline-block;padding:10px 20px;background:#18181b;color:#fff;text-decoration:none;border-radius:6px">Подтвердить email</a></p>
                  <p style="color:#888;font-size:12px;margin-top:24px">Если кнопка не работает, скопируйте ссылку в браузер:</p>
                  <p style="color:#888;font-size:12px;word-break:break-all">{safeLink}</p>
                  <hr style="border:none;border-top:1px solid #eee;margin:24px 0">
                  <p style="color:#aaa;font-size:11px;text-align:center;margin:0">
                    © {year} MCollector &nbsp;·&nbsp;
                    <a href="{frontUrl}" style="color:#aaa;text-decoration:none">{frontUrl}</a>
                  </p>
                </div>
                """;
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
                  <p style="color:#888;font-size:12px;word-break:break-all">{safeLink}</p>
                  <hr style="border:none;border-top:1px solid #eee;margin:24px 0">
                  <p style="color:#aaa;font-size:11px;text-align:center;margin:0">
                    © {year} MCollector &nbsp;·&nbsp;
                    <a href="{frontUrl}" style="color:#aaa;text-decoration:none">{frontUrl}</a>
                  </p>
                </div>
                """;
    }

    private static string DecodeBase64UrlToken(string encoded)
    {
        var bytes = WebEncoders.Base64UrlDecode(encoded);
        return Encoding.UTF8.GetString(bytes);
    }
}
