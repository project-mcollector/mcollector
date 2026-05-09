using Identity.Api.Domain.Entities;
using Identity.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Utilities;

namespace Identity.Api.Application.Services;

public record AuthTokenDto(string AccessToken, double ExpiresIn, string RefreshToken);

public interface IAuthService
{
    Task<Result<AuthTokenDto>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<Result<AuthTokenDto>> RegisterAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<Result<AuthTokenDto>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);
}

public class AuthService(
    UserManager<ApplicationUser> userManager,
    IdentityAppDbContext dbContext,
    IConfiguration configuration,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly string _jwtSecret = configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("Jwt:Secret is not configured");

    private readonly int _accessTokenMinutes = configuration.GetValue<int>("Jwt:ExpiresInMinutes", 60);
    private readonly int _refreshTokenDays = configuration.GetValue<int>("Jwt:RefreshTokenDays", 30);

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
        return await BuildTokenAsync(user, cancellationToken);
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
        return await BuildTokenAsync(user, cancellationToken);
    }

    public async Task<Result<AuthTokenDto>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var token = await dbContext.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.Token == refreshToken, cancellationToken);

        if (token is null || !token.IsActive)
            return Errors.Unauthorized("Invalid or expired refresh token");

        token.RevokedAt = DateTimeOffset.UtcNow;
        return await BuildTokenAsync(token.User, cancellationToken);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var token = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(t => t.Token == refreshToken, cancellationToken);

        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<AuthTokenDto> BuildTokenAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        };

        var jwtToken = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_accessTokenMinutes),
            signingCredentials: new(key, SecurityAlgorithms.HmacSha256)
        );

        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        var refreshTokenValue = Convert.ToBase64String(bytes);

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_refreshTokenDays)
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new(
            new JwtSecurityTokenHandler().WriteToken(jwtToken),
            TimeSpan.FromMinutes(_accessTokenMinutes).TotalSeconds,
            refreshTokenValue
        );
    }
}
