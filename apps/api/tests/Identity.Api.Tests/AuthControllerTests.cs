using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Identity.Api.Api.Controllers;
using Identity.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Identity.Api.Tests;

public class AuthControllerTests
{
    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        var userManager = TestHelpers.CreateUserManagerMock();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "unit-test-secret-key-unit-test-secret-key"
            })
            .Build();

        var user = new ApplicationUser { Id = Guid.NewGuid().ToString(), Email = "user@acme.dev" };
        userManager.Setup(x => x.FindByEmailAsync("user@acme.dev")).ReturnsAsync(user);
        userManager.Setup(x => x.CheckPasswordAsync(user, "bad-password")).ReturnsAsync(false);

        var controller = new AuthController(userManager.Object, configuration);

        var result = await controller.Login(new()
        {
            Email = "user@acme.dev",
            Password = "bad-password"
        });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid credentials", unauthorized.Value);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsJwtWithExpectedClaims()
    {
        var userManager = TestHelpers.CreateUserManagerMock();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "unit-test-secret-key-unit-test-secret-key"
            })
            .Build();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = "user@acme.dev"
        };

        userManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        userManager.Setup(x => x.CheckPasswordAsync(user, "password123")).ReturnsAsync(true);

        var controller = new AuthController(userManager.Object, configuration);

        var result = await controller.Login(new()
        {
            Email = user.Email,
            Password = "password123"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LoginResponse>(ok.Value);

        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.Equal(TimeSpan.FromDays(7).TotalSeconds, response.ExpiresIn);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Email && c.Value == user.Email);
        Assert.True(jwt.ValidTo > DateTime.UtcNow.AddDays(6));
    }
}
