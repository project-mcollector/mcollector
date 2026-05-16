using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Identity.Api.Application.Options;
using Identity.Api.Domain.Entities;
using Identity.Api.Infrastructure.Persistence;
using Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Utilities;

namespace Identity.Api.Application.Services;

public interface ITokenService
{
    Task<AuthTokenDto> CreateTokenAsync(ApplicationUser user, CancellationToken cancellationToken);
}

public class TokenService(
    IdentityAppDbContext dbContext,
    IOptions<JwtOptions> jwtOptions,
    IDateTimeProvider dateTimeProvider) : ITokenService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<AuthTokenDto> CreateTokenAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        await dbContext.RefreshTokens
            .Where(t => t.UserId == user.Id && (t.RevokedAt != null || t.ExpiresAt <= now))
            .ExecuteDeleteAsync(cancellationToken);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
        };

        var jwtToken = new JwtSecurityToken(
            claims: claims,
            expires: now.AddMinutes(_jwt.ExpiresInMinutes),
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
            ExpiresAt = now.AddDays(_jwt.RefreshTokenDays),
            CreatedAt = now,
            User = user
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new(
            new JwtSecurityTokenHandler().WriteToken(jwtToken),
            TimeSpan.FromMinutes(_jwt.ExpiresInMinutes).TotalSeconds,
            refreshTokenValue
        );
    }
}
