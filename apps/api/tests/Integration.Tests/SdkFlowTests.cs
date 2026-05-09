using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Analytics.Api.API.Controllers;
using Analytics.Api.Infrastructure.Persistence;
using Contracts.Messages;
using Infrastructure.Auth;
using Infrastructure.Messaging;
using Ingestion.Api.Controllers;
using Ingestion.Api.Models;
using Ingestion.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text.Encodings.Web;

namespace Integration.Tests;

public class SdkFlowTests
{
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly string _dbName;
    internal static readonly InMemoryDatabaseRoot InMemoryDbRoot = new();
    private const string TestWriteKey = "proj_integration-test-key";
    private const string JwtSecret = "integration-tests-jwt-secret-32-chars-min";

    private readonly HttpClient _ingestionClient;
    private readonly HttpClient _analyticsClient;

    public SdkFlowTests()
    {
        _dbName = $"integration-{_projectId}";

        // Program.cs reads the connection string at module level before ConfigureServices
        // overrides can replace the DbContext, so we must supply a non-null value upfront.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection",
            "Host=localhost;Database=test");

        _ingestionClient = new WebApplicationFactory<IngestionController>()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                ReplaceDbContext<IdentityValidationContext>(services);
                services.RemoveAll<IApiKeyValidator>();
                services.AddSingleton<IApiKeyValidator>(new FakeApiKeyValidator(_projectId, TestWriteKey));
                services.RemoveAll<IEventPublisher>();
                services.AddSingleton<IEventPublisher>(new DirectDbPublisher(_dbName));
            }))
            .CreateClient();

        _analyticsClient = new WebApplicationFactory<AnalyticsController>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, cfg) =>
                    cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Secret"] = JwtSecret }));
                builder.ConfigureServices(ReplaceDbContext<AnalyticsDbContext>);
                builder.ConfigureTestServices(services =>
                {
                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

                    services.AddAuthorizationBuilder()
                        .SetDefaultPolicy(new AuthorizationPolicyBuilder(TestAuthHandler.SchemeName)
                            .RequireAuthenticatedUser()
                            .Build());
                });
            })
            .CreateClient();

        _analyticsClient.DefaultRequestHeaders.Authorization =
            new("Bearer", GenerateJwt());
    }

    [Fact]
    public async Task SdkBatch_EventsAppearInAnalyticsOverview()
    {
        var payload = new SdkBatchRequest
        {
            WriteKey = TestWriteKey,
            Events =
            [
                new() { Event = "$pageview", UserId = "user1", Timestamp = DateTimeOffset.UtcNow },
                new() { Event = "button_click", UserId = "user2", Timestamp = DateTimeOffset.UtcNow },
                new() { Event = "$pageview", UserId = "user1", Timestamp = DateTimeOffset.UtcNow },
            ]
        };

        var ingestResponse = await _ingestionClient.PostAsJsonAsync("/api/v1/ingest/events", payload);
        Assert.Equal(HttpStatusCode.Accepted, ingestResponse.StatusCode);

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddHours(-1).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddHours(1).ToString("O"));

        var overviewResponse = await _analyticsClient.GetAsync(
            $"/api/v1/projects/{_projectId}/analytics/overview?from={from}&to={to}");

        Assert.Equal(HttpStatusCode.OK, overviewResponse.StatusCode);
        var json = await overviewResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, json.GetProperty("totalEvents").GetInt32());
        Assert.Equal(2, json.GetProperty("uniqueUsers").GetInt32());
        Assert.Equal(2, json.GetProperty("pageViews").GetInt32());
    }

    [Fact]
    public async Task SdkBatch_EventNamesAppearInEventsList()
    {
        var payload = new SdkBatchRequest
        {
            WriteKey = TestWriteKey,
            Events =
            [
                new() { Event = "signup", UserId = "user1", Timestamp = DateTimeOffset.UtcNow },
                new() { Event = "signup", UserId = "user2", Timestamp = DateTimeOffset.UtcNow },
                new() { Event = "purchase", UserId = "user1", Timestamp = DateTimeOffset.UtcNow },
            ]
        };

        var ingestResponse = await _ingestionClient.PostAsJsonAsync("/api/v1/ingest/events", payload);
        Assert.Equal(HttpStatusCode.Accepted, ingestResponse.StatusCode);

        var eventsResponse = await _analyticsClient.GetAsync(
            $"/api/v1/projects/{_projectId}/analytics/events");

        Assert.Equal(HttpStatusCode.OK, eventsResponse.StatusCode);
        var events = await eventsResponse.Content.ReadFromJsonAsync<List<string>>();
        Assert.NotNull(events);
        Assert.Contains("signup", events);
        Assert.Contains("purchase", events);
    }

    [Fact]
    public async Task SdkBatch_InvalidWriteKey_ReturnsUnauthorized()
    {
        var payload = new SdkBatchRequest
        {
            WriteKey = "invalid-key",
            Events = [new() { Event = "test", Timestamp = DateTimeOffset.UtcNow }]
        };

        var response = await _ingestionClient.PostAsJsonAsync("/api/v1/ingest/events", payload);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private void ReplaceDbContext<T>(IServiceCollection services) where T : DbContext
    {
        var descriptorsToRemove = services
            .Where(d => d.ServiceType == typeof(T)
                        || d.ServiceType == typeof(DbContextOptions<T>)
                        || d.ServiceType == typeof(DbContextOptions)
                        || d.ServiceType == typeof(IDbContextOptionsConfiguration<T>))
            .ToList();

        foreach (var descriptor in descriptorsToRemove)
            services.Remove(descriptor);

        services.AddDbContext<T>(opt => opt.UseInMemoryDatabase(_dbName, InMemoryDbRoot));
    }

    private static string GenerateJwt()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var token = new JwtSecurityToken(
            claims: [new(ClaimTypes.NameIdentifier, "test-user")],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

file sealed class FakeApiKeyValidator(Guid projectId, string writeKey) : IApiKeyValidator
{
    public Task<bool> ValidateApiKeyAsync(Guid pId, string key) =>
        Task.FromResult(pId == projectId && key == writeKey);

    public Task<Guid?> GetProjectIdByApiKeyAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(key == writeKey ? projectId : (Guid?)null);
}

file sealed class DirectDbPublisher(string dbName) : IEventPublisher
{
    public async Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
    {
        if (message is not RawEvent raw) return;

        var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseInMemoryDatabase(dbName, SdkFlowTests.InMemoryDbRoot)
            .Options;
        await using var db = new AnalyticsDbContext(options);

        db.ProcessedEvents.Add(new()
        {
            EventId = Guid.NewGuid(),
            ProjectId = raw.ProjectId,
            EventName = raw.EventName,
            UserId = raw.UserId ?? "anonymous",
            SessionId = raw.SessionId,
            PropertiesJson = raw.Properties is null ? null : JsonSerializer.Serialize(raw.Properties),
            Timestamp = raw.ClientTimestamp != default ? raw.ClientTimestamp : raw.ServerTimestamp,
            ProcessedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }
}

file sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "integration-user")], SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
