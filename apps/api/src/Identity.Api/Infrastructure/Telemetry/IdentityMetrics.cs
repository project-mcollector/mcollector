using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Identity.Api.Infrastructure.Telemetry;

public sealed class IdentityMetrics : IDisposable
{
    public static class Outcomes
    {
        public const string Success = "success";
        public const string UserNotFound = "user_not_found";
        public const string InvalidCredentials = "invalid_credentials";
        public const string LockedOut = "locked_out";
        public const string EmailUnconfirmed = "email_unconfirmed";
        public const string ReuseDetected = "reuse_detected";
        public const string Invalid = "invalid";
        public const string Failed = "failed";
        public const string LimitReached = "limit_reached";
        public const string AttestationFailed = "attestation_failed";
        public const string AssertionFailed = "assertion_failed";
    }

    private readonly Meter _meter;
    private readonly Counter<long> _loginAttempts;
    private readonly Counter<long> _registrations;
    private readonly Counter<long> _tokenRefreshes;
    private readonly Counter<long> _emailConfirmations;
    private readonly Counter<long> _passwordResets;
    private readonly Counter<long> _passkeyLogins;
    private readonly Counter<long> _passkeyRegistrations;

    public IdentityMetrics()
    {
        _meter = new Meter("identity-api");
        _loginAttempts = _meter.CreateCounter<long>("auth_login_attempts",
            description: "Login attempts by outcome");
        _registrations = _meter.CreateCounter<long>("auth_registrations",
            description: "Registration attempts by outcome");
        _tokenRefreshes = _meter.CreateCounter<long>("auth_token_refreshes",
            description: "Token refresh attempts by outcome");
        _emailConfirmations = _meter.CreateCounter<long>("auth_email_confirmations",
            description: "Email confirmation attempts by outcome");
        _passwordResets = _meter.CreateCounter<long>("auth_password_resets",
            description: "Password reset attempts by outcome");
        _passkeyLogins = _meter.CreateCounter<long>("auth_passkey_logins",
            description: "Passkey login attempts by outcome");
        _passkeyRegistrations = _meter.CreateCounter<long>("auth_passkey_registrations",
            description: "Passkey registration attempts by outcome");
    }

    public void RecordLogin(string outcome) =>
        _loginAttempts.Add(1, new TagList { { "outcome", outcome } });

    public void RecordRegistration(string outcome) =>
        _registrations.Add(1, new TagList { { "outcome", outcome } });

    public void RecordTokenRefresh(string outcome) =>
        _tokenRefreshes.Add(1, new TagList { { "outcome", outcome } });

    public void RecordEmailConfirmation(string outcome) =>
        _emailConfirmations.Add(1, new TagList { { "outcome", outcome } });

    public void RecordPasswordReset(string outcome) =>
        _passwordResets.Add(1, new TagList { { "outcome", outcome } });

    public void RecordPasskeyLogin(string outcome) =>
        _passkeyLogins.Add(1, new TagList { { "outcome", outcome } });

    public void RecordPasskeyRegistration(string outcome) =>
        _passkeyRegistrations.Add(1, new TagList { { "outcome", outcome } });

    public void Dispose() => _meter.Dispose();
}
