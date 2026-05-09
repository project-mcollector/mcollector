using System.Net;
using System.Net.Http.Json;
using Infrastructure.Messaging;
using Infrastructure.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;

namespace Ingestion.Api.Tests;

public class IngestionControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IngestionControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        // Program.cs reads the connection string at module level, before ConfigureServices
        // overrides fire. Provide a dummy value so the guard doesn't throw.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection",
            "Host=localhost;Database=test");

        Mock<IEventPublisher> mockPublisher = new();
        Mock<IApiKeyValidator> mockApiKeyValidator = new();

        mockApiKeyValidator.Setup(v => v.ValidateApiKeyAsync(It.IsAny<Guid>(), It.IsAny<string>()))
                            .ReturnsAsync(true);

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(IEventPublisher));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddScoped<IEventPublisher>(_ => mockPublisher.Object);

                var authDescriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(IApiKeyValidator));
                if (authDescriptor != null)
                    services.Remove(authDescriptor);

                services.AddScoped<IApiKeyValidator>(_ => mockApiKeyValidator.Object);
            });
        });
    }

    [Fact]
    public async Task PostEvent_ValidRequest_Returns202()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Project-Id", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");

        var request = new
        {
            EventName = "page_view",
            UserId = "user1",
            ClientTimestamp = DateTimeOffset.UtcNow
        };

        var response = await client.PostAsJsonAsync("/api/v1/ingest/event", request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task PostEvent_MissingProjectId_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");

        var request = new
        {
            EventName = "page_view",
            UserId = "user1",
            ClientTimestamp = DateTimeOffset.UtcNow
        };

        var response = await client.PostAsJsonAsync("/api/v1/ingest/event", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostEvent_MissingApiKey_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Project-Id", Guid.NewGuid().ToString());

        var request = new
        {
            EventName = "page_view",
            UserId = "user1",
            ClientTimestamp = DateTimeOffset.UtcNow
        };

        var response = await client.PostAsJsonAsync("/api/v1/ingest/event", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostBatch_ValidRequest_Returns202()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Project-Id", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");

        var requests = new[]
        {
            new { EventName = "page_view", UserId = "user1", ClientTimestamp = DateTimeOffset.UtcNow },
            new { EventName = "click", UserId = "user2", ClientTimestamp = DateTimeOffset.UtcNow }
        };

        var response = await client.PostAsJsonAsync("/api/v1/ingest/batch", requests);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task PostBatch_TooManyEvents_Returns400()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Project-Id", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");

        var requests = Enumerable.Range(0, 51).Select(_ => new
        {
            EventName = "page_view",
            UserId = "user1",
            ClientTimestamp = DateTimeOffset.UtcNow
        });

        var response = await client.PostAsJsonAsync("/api/v1/ingest/batch", requests);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetHealth_Returns200()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/ingest/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
