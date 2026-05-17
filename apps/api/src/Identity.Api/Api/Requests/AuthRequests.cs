using System.ComponentModel.DataAnnotations;

namespace Identity.Api.Api.Requests;

public class LoginRequest
{
    [Required]
    [ValidEmail]
    public required string Email { get; init; }

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public required string Password { get; init; }
}

public class RegisterRequest
{
    [Required]
    [ValidEmail]
    public required string Email { get; init; }

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public required string Password { get; init; }

    public string? Locale { get; init; }
}

public class RefreshRequest
{
    [Required] public required string RefreshToken { get; init; }
}

public class RevokeRequest
{
    [Required] public required string RefreshToken { get; init; }
}

public class ResendConfirmationRequest
{
    [Required]
    [ValidEmail]
    public required string Email { get; init; }

    public string? Locale { get; init; }
}

public class ForgotPasswordRequest
{
    [Required]
    [ValidEmail]
    public required string Email { get; init; }

    public string? Locale { get; init; }
}

public class ResetPasswordRequest
{
    [Required] public required string UserId { get; init; }
    [Required] public required string Token { get; init; }

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public required string Password { get; init; }
}

public class PasskeyOptionsRequest
{
    [ValidEmail]
    public string? Email { get; init; }
}

public class PasskeyCredentialRequest
{
    [Required] public required string CredentialJson { get; init; }
}
