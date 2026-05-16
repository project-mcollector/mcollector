using System.ComponentModel.DataAnnotations;

namespace Identity.Api.Application.Options;

public class JwtOptions
{
    public const string Section = "Jwt";

    [Required]
    public string Secret { get; init; } = "";
    public int ExpiresInMinutes { get; init; } = 60;
    public int RefreshTokenDays { get; init; } = 30;
}
