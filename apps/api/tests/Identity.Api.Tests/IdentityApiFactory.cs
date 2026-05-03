using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Identity.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Api.Tests;

public sealed class IdentityApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    internal const string TestJwtSecret = "identity-tests-jwt-secret-must-be-32-chars-min!";
    internal static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public IdentityApiFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Secret", TestJwtSecret);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {

        // Replaces PostgreSQL with InMemory; runs before ConfigureTestServices
        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(IdentityAppDbContext) ||
                    d.ServiceType == typeof(DbContextOptions<IdentityAppDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(IDbContextOptionsConfiguration<IdentityAppDbContext>))
                .ToList();

            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<IdentityAppDbContext>(opt => opt.UseInMemoryDatabase(_dbName));
        });

        // AddSharedAuthentication captures Jwt:Secret at startup before ConfigureAppConfiguration
        // runs, so we override the validation key directly after all services are registered.
        builder.ConfigureTestServices(services =>
        {
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                };
            });
        });
    }

    public async Task<(HttpClient Client, string UserId)> CreateUserAsync(
        string email, string password = "Password1!")
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Password = password });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AuthTokenResponse>(JsonOpts)
            ?? throw new InvalidOperationException("Register returned no body");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken);
        var userId = jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value;

        client.DefaultRequestHeaders.Authorization = new("Bearer", body.AccessToken);
        return (client, userId);
    }

    public async Task<Guid> CreateProjectAsync(HttpClient client, string name, string? description = null)
    {
        var response = await client.PostAsJsonAsync("/api/projects",
            new { Name = name, Description = description });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOpts)
            ?? throw new InvalidOperationException("CreateProject returned no body");
        return body.Id;
    }
}

internal record AuthTokenResponse(string AccessToken, double ExpiresIn);
internal record ProjectJson(Guid Id, string Name, string Description, string ApiKey);
internal record ProjectWithMembersJson(Guid Id, string Name, string Description, string ApiKey, List<MemberJson> Members);
internal record MemberJson(string Id, string Email);
internal record UserProfileJson(string Id, string? Email, string? UserName, List<UserProjectJson> Projects);
internal record UserProjectJson(Guid Id, string Name, string Description);
