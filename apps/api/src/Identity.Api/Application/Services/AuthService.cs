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
    Task<Result<AuthTokenDto>> LoginAsync(string email, string password);
    Task<Result<AuthTokenDto>> RegisterAsync(string email, string password);
}

public class AuthService(
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<Result<AuthTokenDto>> LoginAsync(string email, string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !await userManager.CheckPasswordAsync(user, password))
        {
            logger.LogWarning("Failed login attempt for {Email}", email);
            return Errors.Unauthorized(email);
        }

        logger.LogInformation("User {UserId} logged in", user.Id);
        return BuildToken(user);
    }

    public async Task<Result<AuthTokenDto>> RegisterAsync(string email, string password)
    {
        var user = new ApplicationUser { Email = email, UserName = email };
        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            logger.LogWarning("Registration failed for {Email}: {Errors}", email, errors);
            return Errors.Validation("Registration", errors);
        }

        logger.LogInformation("User {UserId} registered with email {Email}", user.Id, email);
        return BuildToken(user);
    }

    private AuthTokenDto BuildToken(ApplicationUser user)
    {
        var secret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
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
