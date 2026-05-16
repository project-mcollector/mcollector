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

    private static AnalyticsController CreateController(AnalyticsDbContext dbContext)
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

    // Helper: serialize controller anonymous-object result value to JsonDocument for assertions
    private static JsonDocument ToDoc(object? value) =>
        JsonDocument.Parse(JsonSerializer.Serialize(value));

    // -------------------------------------------------------------------------
    // GetOverview
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOverview_ReturnsCorrectCounts()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "$pageview",    UserId = "user1", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "$pageview",    UserId = "user1", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "custom_event", UserId = "user2", ProcessedAt = DateTimeOffset.UtcNow },
            // different project — must not appear
            new() { EventId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), EventName = "$pageview", UserId = "user3", ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetOverview(projectId, null, null, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var doc = ToDoc(result.Value);
        Assert.Equal(3, doc.RootElement.GetProperty("totalEvents").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("uniqueUsers").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("pageViews").GetInt32());
    }

    [Fact]
    public async Task GetOverview_UnknownProject_ReturnsZeros()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();

        // Seed events for a different project only
        dbContext.ProcessedEvents.Add(new()
        {
            EventId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            EventName = "$pageview",
            UserId = "user1",
            ProcessedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetOverview(projectId, null, null, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var doc = ToDoc(result.Value);
        Assert.Equal(0, doc.RootElement.GetProperty("totalEvents").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("uniqueUsers").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("pageViews").GetInt32());
    }

    [Fact]
    public async Task GetOverview_FromFilter_ExcludesOlderEvents()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();
        var cutoff = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "$pageview", UserId = "u1",
                Timestamp = new DateTimeOffset(2024, 5, 31, 0, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "$pageview", UserId = "u2",
                Timestamp = new DateTimeOffset(2024, 6, 2, 0, 0, 0, TimeSpan.Zero),  ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetOverview(projectId, from: cutoff, to: null, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var doc = ToDoc(result.Value);
        Assert.Equal(1, doc.RootElement.GetProperty("totalEvents").GetInt32());
    }

    [Fact]
    public async Task GetOverview_ToFilter_ExcludesNewerEvents()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();
        var cutoff = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "$pageview", UserId = "u1",
                Timestamp = new DateTimeOffset(2024, 5, 31, 0, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "$pageview", UserId = "u2",
                Timestamp = new DateTimeOffset(2024, 6, 2, 0, 0, 0, TimeSpan.Zero),  ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetOverview(projectId, from: null, to: cutoff, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var doc = ToDoc(result.Value);
        Assert.Equal(1, doc.RootElement.GetProperty("totalEvents").GetInt32());
    }

    [Fact]
    public async Task GetOverview_ProjectIsolation_DoesNotLeakOtherProjectData()
    {
        var dbContext = GetDbContext();
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectA, EventName = "$pageview", UserId = "uA", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectB, EventName = "$pageview", UserId = "uB", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectB, EventName = "$pageview", UserId = "uB2", ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetOverview(projectA, null, null, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var doc = ToDoc(result.Value);
        Assert.Equal(1, doc.RootElement.GetProperty("totalEvents").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("uniqueUsers").GetInt32());
    }

    // -------------------------------------------------------------------------
    // GetEvents
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetEvents_ReturnsDistinctEventNames()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "$pageview",    UserId = "user1", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "$pageview",    UserId = "user1", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "custom_event", UserId = "user2", ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetEvents(projectId, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var events = Assert.IsType<List<string>>(result.Value);
        Assert.Equal(2, events.Count);
        Assert.Contains("$pageview", events);
        Assert.Contains("custom_event", events);
    }

    [Fact]
    public async Task GetEvents_UnknownProject_ReturnsEmptyList()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();

        // Seed events for a different project
        dbContext.ProcessedEvents.Add(new()
        {
            EventId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            EventName = "$pageview",
            UserId = "u1",
            ProcessedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetEvents(projectId, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var events = Assert.IsType<List<string>>(result.Value);
        Assert.Empty(events);
    }

    [Fact]
    public async Task GetEvents_ProjectIsolation_DoesNotReturnOtherProjectEventNames()
    {
        var dbContext = GetDbContext();
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectA, EventName = "event_a", UserId = "u1", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectB, EventName = "event_b", UserId = "u2", ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetEvents(projectA, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var events = Assert.IsType<List<string>>(result.Value);
        Assert.Single(events);
        Assert.Contains("event_a", events);
        Assert.DoesNotContain("event_b", events);
    }

    // -------------------------------------------------------------------------
    // GetEventProperties
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetEventProperties_ReturnsDistinctProperties()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "$pageview", UserId = "user1",
                PropertiesJson = "{\"url\": \"/home\", \"referrer\": \"google\"}", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "$pageview", UserId = "user2",
                PropertiesJson = "{\"url\": \"/about\", \"duration\": 10}", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "other_event", UserId = "user1",
                PropertiesJson = "{\"other_prop\": true}", ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetEventProperties(projectId, "$pageview", CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var properties = Assert.IsType<HashSet<string>>(result.Value);
        Assert.Equal(3, properties.Count);
        Assert.Contains("url", properties);
        Assert.Contains("referrer", properties);
        Assert.Contains("duration", properties);
        Assert.DoesNotContain("other_prop", properties);
    }

    [Fact]
    public async Task GetEventProperties_MalformedJson_IsSkipped()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "click", UserId = "u1",
                PropertiesJson = "NOT_VALID_JSON", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "click", UserId = "u2",
                PropertiesJson = "{\"valid_key\": 1}", ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        // Should not throw; malformed entry is silently skipped
        var result = await CreateController(dbContext)
            .GetEventProperties(projectId, "click", CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var properties = Assert.IsType<HashSet<string>>(result.Value);
        Assert.Single(properties);
        Assert.Contains("valid_key", properties);
    }

    [Fact]
    public async Task GetEventProperties_NonObjectJson_IsSkipped()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();

        dbContext.ProcessedEvents.AddRange(
        [
            // JSON array — not an object; should be skipped
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "click", UserId = "u1",
                PropertiesJson = "[1, 2, 3]", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "click", UserId = "u2",
                PropertiesJson = "{\"good_prop\": true}", ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetEventProperties(projectId, "click", CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var properties = Assert.IsType<HashSet<string>>(result.Value);
        Assert.Single(properties);
        Assert.Contains("good_prop", properties);
    }

    [Fact]
    public async Task GetEventProperties_NullPropertiesJson_IsExcluded()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "click", UserId = "u1",
                PropertiesJson = null, ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "click", UserId = "u2",
                PropertiesJson = "{\"prop_a\": 1}", ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetEventProperties(projectId, "click", CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var properties = Assert.IsType<HashSet<string>>(result.Value);
        Assert.Single(properties);
        Assert.Contains("prop_a", properties);
    }

    [Fact]
    public async Task GetEventProperties_SampleSizeCap_ReturnsAtMost100Events()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();

        // Seed 105 events. Each has a unique property named "key_<N>".
        // The endpoint takes the 100 most recent by Timestamp.
        var events = Enumerable.Range(0, 105).Select(i => new ProcessedEvent
        {
            EventId = Guid.NewGuid(),
            ProjectId = projectId,
            EventName = "load",
            UserId = "u",
            PropertiesJson = $"{{\"key_{i}\": {i}}}",
            // Timestamps in ascending order so the last 100 by descending have keys 5..104
            Timestamp = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(i),
            ProcessedAt = DateTimeOffset.UtcNow
        }).ToList();

        dbContext.ProcessedEvents.AddRange(events);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetEventProperties(projectId, "load", CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var properties = Assert.IsType<HashSet<string>>(result.Value);
        // Exactly 100 distinct keys (keys 5..104 are returned; keys 0..4 are cut off)
        Assert.Equal(100, properties.Count);
    }

    // -------------------------------------------------------------------------
    // GetEventCounts
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetEventCounts_ReturnsCountsOrderedDescending()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "rare",   UserId = "u1", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "common", UserId = "u1", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "common", UserId = "u2", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "common", UserId = "u3", ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetEventCounts(projectId, null, null, null, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var doc = ToDoc(result.Value);
        var items = doc.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        // First (highest) must be "common" with count 3
        Assert.Equal("common", items[0].GetProperty("name").GetString());
        Assert.Equal(3, items[0].GetProperty("count").GetInt32());
        Assert.Equal("rare", items[1].GetProperty("name").GetString());
        Assert.Equal(1, items[1].GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task GetEventCounts_UnknownProject_ReturnsEmptyList()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();

        dbContext.ProcessedEvents.Add(new()
        {
            EventId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            EventName = "$pageview",
            UserId = "u1",
            ProcessedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetEventCounts(projectId, null, null, null, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var doc = ToDoc(result.Value);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task GetEventCounts_FromToFilter_NarrowsResults()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();
        var windowStart = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var windowEnd = new DateTimeOffset(2024, 3, 31, 0, 0, 0, TimeSpan.Zero);

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "in_window", UserId = "u",
                Timestamp = new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "out_of_window", UserId = "u",
                Timestamp = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetEventCounts(projectId, from: windowStart, to: windowEnd, period: null, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var doc = ToDoc(result.Value);
        Assert.Equal(1, doc.RootElement.GetArrayLength());
        Assert.Equal("in_window", doc.RootElement[0].GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("day")]
    [InlineData("week")]
    [InlineData("month")]
    public async Task GetEventCounts_PeriodOverridesFromTo(string period)
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();

        // Seed one event with default Timestamp (DateTimeOffset.MinValue — very old)
        // and one event with UtcNow (within the period window).
        var recent = DateTimeOffset.UtcNow.AddHours(-1);
        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "recent", UserId = "u",
                Timestamp = recent, ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "ancient", UserId = "u",
                Timestamp = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        // Pass a very old from that would include the ancient event — period should override it
        var ancientFrom = new DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var result = await CreateController(dbContext)
            .GetEventCounts(projectId, from: ancientFrom, to: null, period: period, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var doc = ToDoc(result.Value);
        // "ancient" should NOT appear because period overrides from/to to last day/week/month
        var names = doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("name").GetString())
            .ToList();
        Assert.DoesNotContain("ancient", names);
        Assert.Contains("recent", names);
    }

    [Fact]
    public async Task GetEventCounts_ProjectIsolation_DoesNotLeakOtherProject()
    {
        var dbContext = GetDbContext();
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectA, EventName = "event_a", UserId = "u", ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectB, EventName = "event_b", UserId = "u", ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetEventCounts(projectA, null, null, null, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var doc = ToDoc(result.Value);
        var names = doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("event_a", names);
        Assert.DoesNotContain("event_b", names);
    }

    // -------------------------------------------------------------------------
    // GetEventsTimeseries
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("invalid")]
    [InlineData("week")]
    [InlineData("minute")]
    [InlineData("")]
    public async Task GetEventsTimeseries_InvalidInterval_ReturnsBadRequest(string interval)
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();
        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to = DateTimeOffset.UtcNow;

        var result = await CreateController(dbContext)
            .GetEventsTimeseries(projectId, from, to, interval, null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData("hour")]
    [InlineData("day")]
    [InlineData("month")]
    public async Task GetEventsTimeseries_ValidInterval_ReturnsOk(string interval)
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();
        var from = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero);

        dbContext.ProcessedEvents.Add(new()
        {
            EventId = Guid.NewGuid(),
            ProjectId = projectId,
            EventName = "$pageview",
            UserId = "u",
            Timestamp = new DateTimeOffset(2024, 1, 10, 5, 0, 0, TimeSpan.Zero),
            ProcessedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetEventsTimeseries(projectId, from, to, interval, null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetEventsTimeseries_DayInterval_BucketsCorrectly()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();
        var from = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 1, 31, 23, 59, 59, TimeSpan.Zero);

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "e", UserId = "u",
                Timestamp = new DateTimeOffset(2024, 1, 5, 10, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "e", UserId = "u",
                Timestamp = new DateTimeOffset(2024, 1, 5, 15, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "e", UserId = "u",
                Timestamp = new DateTimeOffset(2024, 1, 10, 0, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetEventsTimeseries(projectId, from, to, "day", null, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var doc = ToDoc(result.Value);
        var buckets = doc.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, buckets.Count);
        Assert.Equal(2, buckets[0].GetProperty("count").GetInt32()); // Jan 5
        Assert.Equal(1, buckets[1].GetProperty("count").GetInt32()); // Jan 10
    }

    [Fact]
    public async Task GetEventsTimeseries_HourInterval_BucketsCorrectly()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();
        var from = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 1, 5, 23, 59, 59, TimeSpan.Zero);

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "e", UserId = "u",
                Timestamp = new DateTimeOffset(2024, 1, 5, 10, 15, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "e", UserId = "u",
                Timestamp = new DateTimeOffset(2024, 1, 5, 10, 45, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "e", UserId = "u",
                Timestamp = new DateTimeOffset(2024, 1, 5, 11, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetEventsTimeseries(projectId, from, to, "hour", null, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var doc = ToDoc(result.Value);
        var buckets = doc.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, buckets.Count);
        Assert.Equal(2, buckets[0].GetProperty("count").GetInt32()); // hour 10
        Assert.Equal(1, buckets[1].GetProperty("count").GetInt32()); // hour 11
    }

    [Fact]
    public async Task GetEventsTimeseries_MonthInterval_BucketsCorrectly()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();
        var from = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 3, 31, 23, 59, 59, TimeSpan.Zero);

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "e", UserId = "u",
                Timestamp = new DateTimeOffset(2024, 1, 10, 0, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "e", UserId = "u",
                Timestamp = new DateTimeOffset(2024, 1, 20, 0, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "e", UserId = "u",
                Timestamp = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetEventsTimeseries(projectId, from, to, "month", null, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var doc = ToDoc(result.Value);
        var buckets = doc.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, buckets.Count);
        Assert.Equal(2, buckets[0].GetProperty("count").GetInt32()); // Jan
        Assert.Equal(1, buckets[1].GetProperty("count").GetInt32()); // Feb
    }

    [Fact]
    public async Task GetEventsTimeseries_EventNameFilter_NarrowsResults()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();
        var from = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 1, 31, 23, 59, 59, TimeSpan.Zero);

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "target", UserId = "u",
                Timestamp = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "other", UserId = "u",
                Timestamp = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetEventsTimeseries(projectId, from, to, "day", "target", CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var doc = ToDoc(result.Value);
        var buckets = doc.RootElement.EnumerateArray().ToList();
        Assert.Single(buckets);
        Assert.Equal(1, buckets[0].GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task GetEventsTimeseries_CaseInsensitiveInterval_IsAccepted()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();
        var from = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 1, 31, 23, 59, 59, TimeSpan.Zero);

        var result = await CreateController(dbContext)
            .GetEventsTimeseries(projectId, from, to, "Day", null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetEventsTimeseries_ProjectIsolation_DoesNotLeakOtherProject()
    {
        var dbContext = GetDbContext();
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        var from = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 1, 31, 23, 59, 59, TimeSpan.Zero);

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectA, EventName = "e", UserId = "u",
                Timestamp = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectB, EventName = "e", UserId = "u",
                Timestamp = new DateTimeOffset(2024, 1, 10, 0, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetEventsTimeseries(projectA, from, to, "day", null, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var doc = ToDoc(result.Value);
        var buckets = doc.RootElement.EnumerateArray().ToList();
        // Only projectA's event on Jan 5 should appear
        Assert.Single(buckets);
        Assert.Equal(1, buckets[0].GetProperty("count").GetInt32());
    }

    // -------------------------------------------------------------------------
    // GetUsersTimeseries
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("invalid")]
    [InlineData("week")]
    [InlineData("second")]
    public async Task GetUsersTimeseries_InvalidInterval_ReturnsBadRequest(string interval)
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();
        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to = DateTimeOffset.UtcNow;

        var result = await CreateController(dbContext)
            .GetUsersTimeseries(projectId, from, to, interval, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData("hour")]
    [InlineData("day")]
    [InlineData("month")]
    public async Task GetUsersTimeseries_ValidInterval_ReturnsOk(string interval)
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();
        var from = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero);

        dbContext.ProcessedEvents.Add(new()
        {
            EventId = Guid.NewGuid(),
            ProjectId = projectId,
            EventName = "e",
            UserId = "u",
            Timestamp = new DateTimeOffset(2024, 1, 10, 0, 0, 0, TimeSpan.Zero),
            ProcessedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetUsersTimeseries(projectId, from, to, interval, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetUsersTimeseries_DayInterval_CountsDistinctUsersPerBucket()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();
        var from = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 1, 31, 23, 59, 59, TimeSpan.Zero);

        // Jan 5: 3 events from 2 distinct users
        // Jan 10: 1 event from 1 user
        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "e", UserId = "user_a",
                Timestamp = new DateTimeOffset(2024, 1, 5, 9, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "e", UserId = "user_a",
                Timestamp = new DateTimeOffset(2024, 1, 5, 10, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "e", UserId = "user_b",
                Timestamp = new DateTimeOffset(2024, 1, 5, 11, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "e", UserId = "user_c",
                Timestamp = new DateTimeOffset(2024, 1, 10, 0, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetUsersTimeseries(projectId, from, to, "day", CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var doc = ToDoc(result.Value);
        var buckets = doc.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, buckets.Count);
        Assert.Equal(2, buckets[0].GetProperty("count").GetInt32()); // Jan 5: 2 distinct users
        Assert.Equal(1, buckets[1].GetProperty("count").GetInt32()); // Jan 10: 1 user
    }

    [Fact]
    public async Task GetUsersTimeseries_HourInterval_CountsDistinctUsersPerHour()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();
        var from = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 1, 5, 23, 59, 59, TimeSpan.Zero);

        dbContext.ProcessedEvents.AddRange(
        [
            // Hour 10: 3 events, 2 distinct users
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "e", UserId = "ua",
                Timestamp = new DateTimeOffset(2024, 1, 5, 10, 10, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "e", UserId = "ua",
                Timestamp = new DateTimeOffset(2024, 1, 5, 10, 30, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "e", UserId = "ub",
                Timestamp = new DateTimeOffset(2024, 1, 5, 10, 50, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetUsersTimeseries(projectId, from, to, "hour", CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var doc = ToDoc(result.Value);
        var buckets = doc.RootElement.EnumerateArray().ToList();
        Assert.Single(buckets);
        Assert.Equal(2, buckets[0].GetProperty("count").GetInt32()); // 2 distinct users in hour 10
    }

    [Fact]
    public async Task GetUsersTimeseries_MonthInterval_CountsDistinctUsersPerMonth()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();
        var from = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 2, 29, 23, 59, 59, TimeSpan.Zero);

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "e", UserId = "u1",
                Timestamp = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "e", UserId = "u2",
                Timestamp = new DateTimeOffset(2024, 1, 10, 0, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectId, EventName = "e", UserId = "u1",
                Timestamp = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetUsersTimeseries(projectId, from, to, "month", CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var doc = ToDoc(result.Value);
        var buckets = doc.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, buckets.Count);
        Assert.Equal(2, buckets[0].GetProperty("count").GetInt32()); // Jan: u1 + u2
        Assert.Equal(1, buckets[1].GetProperty("count").GetInt32()); // Feb: u1 only
    }

    [Fact]
    public async Task GetUsersTimeseries_CaseInsensitiveInterval_IsAccepted()
    {
        var dbContext = GetDbContext();
        var projectId = Guid.NewGuid();
        var from = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 1, 31, 23, 59, 59, TimeSpan.Zero);

        var result = await CreateController(dbContext)
            .GetUsersTimeseries(projectId, from, to, "Month", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetUsersTimeseries_ProjectIsolation_DoesNotLeakOtherProject()
    {
        var dbContext = GetDbContext();
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        var from = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 1, 31, 23, 59, 59, TimeSpan.Zero);

        dbContext.ProcessedEvents.AddRange(
        [
            new() { EventId = Guid.NewGuid(), ProjectId = projectA, EventName = "e", UserId = "uA",
                Timestamp = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectB, EventName = "e", UserId = "uB1",
                Timestamp = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
            new() { EventId = Guid.NewGuid(), ProjectId = projectB, EventName = "e", UserId = "uB2",
                Timestamp = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero), ProcessedAt = DateTimeOffset.UtcNow },
        ]);
        await dbContext.SaveChangesAsync();

        var result = await CreateController(dbContext)
            .GetUsersTimeseries(projectA, from, to, "day", CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var doc = ToDoc(result.Value);
        var buckets = doc.RootElement.EnumerateArray().ToList();
        Assert.Single(buckets);
        // Only projectA user counts — should be 1, not 3
        Assert.Equal(1, buckets[0].GetProperty("count").GetInt32());
    }
}
