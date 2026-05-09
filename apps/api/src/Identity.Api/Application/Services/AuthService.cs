using Identity.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Utilities;

namespace Identity.Api.Application.Services;

public record AuthTokenDto(string AccessToken, double ExpiresIn);

public interface IAuthService
{
    Task<Result<AuthTokenDto>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<Result<AuthTokenDto>> RegisterAsync(string email, string password, CancellationToken cancellationToken = default);
}

public class AuthService(
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly string _jwtSecret = configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("Jwt:Secret is not configured");

    public async Task<Result<AuthTokenDto>> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
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
        return BuildToken(user);
    }

    public async Task<Result<AuthTokenDto>> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
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
        return BuildToken(user);
    }

    private AuthTokenDto BuildToken(ApplicationUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: new(key, SecurityAlgorithms.HmacSha256)
        );

        return new(
            new JwtSecurityTokenHandler().WriteToken(token),
            TimeSpan.FromDays(7).TotalSeconds
        );
    }
}
