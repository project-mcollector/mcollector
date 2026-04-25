using System.Net;
using System.Net.Http.Json;
using Contracts.Messages;
using Ingestion.Api.Services;
using Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;

namespace Ingestion.Api.Tests;

public class IngestionControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IEventPublisher> _mockPublisher;

    public IngestionControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _mockPublisher = new Mock<IEventPublisher>();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(IEventPublisher));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddScoped<IEventPublisher>(_ => _mockPublisher.Object);
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
    public async Task PostEvent_MissingProjectId_Returns400()
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

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostEvent_MissingApiKey_Returns400()
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

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
