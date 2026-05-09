using Analytics.Api.API.Controllers;
using Analytics.Api.Infrastructure.Persistence;
using Contracts.Messages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Analytics.Api.Tests;

public class AnalyticsControllerTests
{
    private const string TestUserId = "test-user";

    private static AnalyticsDbContext GetDbContext() =>
        new(new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AnalyticsController CreateController(AnalyticsDbContext dbContext, Guid projectId)
    {
        var controller = new AnalyticsController(dbContext);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, TestUserId)], "test"))
            }
        };
        return controller;
    }

    [Fact]
    public async Task GetOverview_ReturnsCorrectCounts()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "$pageview", UserId = "user1", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "$pageview", UserId = "user1", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "custom_event", UserId = "user2", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), EventName = "$pageview", UserId = "user3", ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext, projectId)
            .GetOverview(projectId, null, null, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var doc = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        Assert.Equal(3, doc.RootElement.GetProperty("totalEvents").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("uniqueUsers").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("pageViews").GetInt32());
    }

    [Fact]
    public async Task GetEvents_ReturnsDistinctEventNames()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "$pageview", UserId = "user1", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "$pageview", UserId = "user1", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "custom_event", UserId = "user2", ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext, projectId)
            .GetEvents(projectId, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var events = Assert.IsType<List<string>>(result.Value);
        Assert.Equal(2, events.Count);
        Assert.Contains("$pageview", events);
        Assert.Contains("custom_event", events);
    }

    [Fact]
    public async Task GetEventProperties_ReturnsDistinctProperties()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "$pageview", UserId = "user1", PropertiesJson = "{\"url\": \"/home\", \"referrer\": \"google\"}", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "$pageview", UserId = "user2", PropertiesJson = "{\"url\": \"/about\", \"duration\": 10}", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "other_event", UserId = "user1", PropertiesJson = "{\"other_prop\": true}", ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext, projectId)
            .GetEventProperties(projectId, "$pageview", CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var properties = Assert.IsType<HashSet<string>>(result.Value);
        Assert.Equal(3, properties.Count);
        Assert.Contains("url", properties);
        Assert.Contains("referrer", properties);
        Assert.Contains("duration", properties);
    }
}
