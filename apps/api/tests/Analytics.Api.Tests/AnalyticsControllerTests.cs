using Analytics.Api.API.Controllers;
using Analytics.Api.Infrastructure.Persistence;
using Contracts.Messages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Analytics.Api.Tests;

public class AnalyticsControllerTests
{
    private static AnalyticsDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new(options);
    }

    [Fact]
    public async Task GetOverview_ReturnsCorrectCounts()
    {
        // Arrange
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();

        dbContext.ProcessedEvents.AddRange(new List<ProcessedEvent>
        {
            new()
            {
                EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "page_view", UserId = "user1",
                ProcessedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "page_view", UserId = "user1",
                ProcessedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "custom_event", UserId = "user2",
                ProcessedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                EventId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), EventName = "page_view", UserId = "user3",
                ProcessedAt = DateTimeOffset.UtcNow
            }, // Different project
        });
        await dbContext.SaveChangesAsync();

        var controller = new AnalyticsController(dbContext);

        // Act
        var result = await controller.GetOverview(projectId, null, null) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        var value = result.Value;

        var json = JsonSerializer.Serialize(value);
        var jsonDocument = JsonDocument.Parse(json);

        Assert.Equal(3, jsonDocument.RootElement.GetProperty("totalEvents").GetInt32());
        Assert.Equal(2, jsonDocument.RootElement.GetProperty("uniqueUsers").GetInt32());
        Assert.Equal(2, jsonDocument.RootElement.GetProperty("pageViews").GetInt32());
    }

    [Fact]
    public async Task GetEvents_ReturnsDistinctEventNames()
    {
        // Arrange
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();

        dbContext.ProcessedEvents.AddRange(new List<ProcessedEvent>
        {
            new()
            {
                EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "page_view", UserId = "user1",
                ProcessedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "page_view", UserId = "user1",
                ProcessedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "custom_event", UserId = "user2",
                ProcessedAt = DateTimeOffset.UtcNow
            },
        });
        await dbContext.SaveChangesAsync();

        var controller = new AnalyticsController(dbContext);

        // Act
        var result = await controller.GetEvents(projectId) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        var events = Assert.IsType<List<string>>(result.Value);
        Assert.Equal(2, events.Count);
        Assert.Contains("page_view", events);
        Assert.Contains("custom_event", events);
    }

    [Fact]
    public async Task GetEventProperties_ReturnsDistinctProperties()
    {
        // Arrange
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();

        dbContext.ProcessedEvents.AddRange(new List<ProcessedEvent>
        {
            new()
            {
                EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "page_view", UserId = "user1",
                PropertiesJson = "{\"url\": \"/home\", \"referrer\": \"google\"}", ProcessedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "page_view", UserId = "user2",
                PropertiesJson = "{\"url\": \"/about\", \"duration\": 10}", ProcessedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "other_event", UserId = "user1",
                PropertiesJson = "{\"other_prop\": true}", ProcessedAt = DateTimeOffset.UtcNow
            },
        });
        await dbContext.SaveChangesAsync();

        var controller = new AnalyticsController(dbContext);

        // Act
        var result = await controller.GetEventProperties(projectId, "page_view") as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        var properties = Assert.IsType<HashSet<string>>(result.Value);
        Assert.Equal(3, properties.Count);
        Assert.Contains("url", properties);
        Assert.Contains("referrer", properties);
        Assert.Contains("duration", properties);
    }
}
