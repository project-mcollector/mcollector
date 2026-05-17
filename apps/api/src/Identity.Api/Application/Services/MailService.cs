using System.Globalization;
using System.Text;
using Identity.Api.Application.Options;
using Identity.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
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

    Task<Result> SendConfirmationEmailAsync(ApplicationUser user, string? locale = null,
        CancellationToken cancellationToken = default);

    Task<Result> SendPasswordResetEmailAsync(ApplicationUser user, string? locale = null,
        CancellationToken cancellationToken = default);
}

public class MailService(
    UserManager<ApplicationUser> userManager,
    IResend emailService,
    IOptions<MailOptions> mailOptions,
    IDateTimeProvider dateTimeProvider,
    IStringLocalizer<MailService> localizer,
    ILogger<MailService> logger)
    : IMailService
{
    // Add the locale code here and create a matching MailService.{code}.resx file to support a new language.
    private static readonly HashSet<string> _supportedLocales = ["en", "ru", "de", "fr", "es", "zh"];

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

    public async Task<Result> SendConfirmationEmailAsync(ApplicationUser user, string? locale = null,
        CancellationToken cancellationToken = default)
    {
        if (user.Email is null)
            return Errors.Validation("Email Confirmation", "User does not have an email");

        var lang = NormalizeLocale(locale);
        var emailToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(emailToken));
        var link = $"{_frontendUrl.TrimEnd('/')}/{lang}/confirm-email?userId={user.Id}&token={encodedToken}";

        using var _ = new CultureScope(lang);
        return await SendEmailAsync(user.Email,
            localizer["ConfirmSubject"],
            BuildEmailHtml(link, _frontendUrl, dateTimeProvider.UtcNow.Year,
                localizer["ConfirmBody"], localizer["ConfirmButton"], localizer["LinkFallback"]),
            cancellationToken);
    }

    public async Task<Result> SendPasswordResetEmailAsync(ApplicationUser user, string? locale = null,
        CancellationToken cancellationToken = default)
    {
        if (user.Email is null)
            return Errors.Validation("Email Confirmation", "User does not have an email");

        var lang = NormalizeLocale(locale);
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(resetToken));
        var link = $"{_frontendUrl.TrimEnd('/')}/{lang}/reset-password?userId={user.Id}&token={encodedToken}";

        using var _ = new CultureScope(lang);
        return await SendEmailAsync(user.Email,
            localizer["ResetSubject"],
            BuildEmailHtml(link, _frontendUrl, dateTimeProvider.UtcNow.Year,
                localizer["ResetBody"], localizer["ResetButton"], localizer["LinkFallback"],
                localizer["ResetIgnore"]),
            cancellationToken);
    }

    private static string NormalizeLocale(string? locale) =>
        locale is not null && _supportedLocales.Contains(locale.ToLowerInvariant())
            ? locale.ToLowerInvariant() : "en";

    private static string BuildEmailHtml(
        string link, string frontUrl, int year,
        string body, string buttonLabel, string fallback, string? extraNote = null)
    {
        var safeLink = link.Replace("&", "&amp;");
        var extraParagraph = extraNote is not null
            ? $"""<p style="color:#888;font-size:12px;margin-top:24px">{extraNote}</p>"""
            : "";
        return $"""
                <div style="font-family:Arial,sans-serif;max-width:480px;margin:0 auto;color:#1a1a1a">
                  <p>{body}</p>
                  <p><a href="{safeLink}" style="display:inline-block;padding:10px 20px;background:#18181b;color:#fff;text-decoration:none;border-radius:6px">{buttonLabel}</a></p>
                  {extraParagraph}
                  <p style="color:#888;font-size:12px">{fallback}</p>
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

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _prev;
        private readonly CultureInfo _prevUI;

        public CultureScope(string locale)
        {
            _prev = CultureInfo.CurrentCulture;
            _prevUI = CultureInfo.CurrentUICulture;
            var culture = CultureInfo.GetCultureInfo(locale);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _prev;
            CultureInfo.CurrentUICulture = _prevUI;
        }
    }
}
