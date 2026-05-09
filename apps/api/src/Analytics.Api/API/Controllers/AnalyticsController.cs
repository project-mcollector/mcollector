using Analytics.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Analytics.Api.API.Controllers;

[ApiController]
[Route("api/v1/projects/{projectId:guid}/analytics")]
[Authorize]
public class AnalyticsController(AnalyticsDbContext dbContext) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(
        Guid projectId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ProcessedEvents.AsNoTracking().Where(e => e.ProjectId == projectId);

        if (from.HasValue) query = query.Where(e => e.Timestamp >= from.Value);
        if (to.HasValue) query = query.Where(e => e.Timestamp <= to.Value);

        var totalEvents = await query.CountAsync(cancellationToken);
        var uniqueUsers = await query.Select(e => e.UserId).Distinct().CountAsync(cancellationToken);
        var pageViews = await query.Where(e => e.EventName == "$pageview").CountAsync(cancellationToken);

        return Ok(new { totalEvents, uniqueUsers, pageViews });
    }

    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(Guid projectId, CancellationToken cancellationToken)
    {
        var rawEvents = await dbContext.ProcessedEvents
            .AsNoTracking()
            .Where(e => e.ProjectId == projectId)
            .Select(e => e.EventName)
            .Distinct()
            .ToListAsync(cancellationToken);

        return Ok(rawEvents);
    }

    [HttpGet("events/{eventName}/properties")]
    public async Task<IActionResult> GetEventProperties(Guid projectId, string eventName, CancellationToken cancellationToken)
    {
        const int sampleSize = 100;

        var jsonStrings = await dbContext.ProcessedEvents
            .AsNoTracking()
            .Where(e => e.ProjectId == projectId && e.EventName == eventName && e.PropertiesJson != null)
            .OrderByDescending(e => e.Timestamp)
            .Select(e => e.PropertiesJson)
            .Take(sampleSize)
            .ToListAsync(cancellationToken);

        var properties = new HashSet<string>();
        foreach (var json in jsonStrings.Where(json => !string.IsNullOrWhiteSpace(json)))
        {
            try
            {
                using var document = JsonDocument.Parse(json ?? string.Empty);
                if (document.RootElement.ValueKind != JsonValueKind.Object) continue;

                foreach (var property in document.RootElement.EnumerateObject())
                    properties.Add(property.Name);
            }
            catch (JsonException)
            {
                // skip malformed JSON blobs
            }
        }

        return Ok(properties);
    }

    [HttpGet("events/counts")]
    public async Task<IActionResult> GetEventCounts(
        Guid projectId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? period,
        CancellationToken cancellationToken)
    {
        if (period is "day" or "week" or "month")
        {
            var now = DateTimeOffset.UtcNow;
            to = now;
            from = period switch
            {
                "day" => now.AddDays(-1),
                "week" => now.AddDays(-7),
                _ => now.AddDays(-30),
            };
        }

        var query = dbContext.ProcessedEvents.AsNoTracking().Where(e => e.ProjectId == projectId);

        if (from.HasValue) query = query.Where(e => e.Timestamp >= from.Value);
        if (to.HasValue) query = query.Where(e => e.Timestamp <= to.Value);

        var counts = await query
            .GroupBy(e => e.EventName)
            .Select(g => new { name = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToListAsync(cancellationToken);

        return Ok(counts);
    }

    [HttpGet("events/timeseries")]
    public async Task<IActionResult> GetEventsTimeseries(
        Guid projectId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] string interval = "day",
        [FromQuery] string? eventName = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidInterval(interval))
            return BadRequest(new { error = $"Invalid interval '{interval}'. Allowed values: hour, day, month." });

        var query = dbContext.ProcessedEvents
            .AsNoTracking()
            .Where(e => e.ProjectId == projectId && e.Timestamp >= from && e.Timestamp <= to);

        if (!string.IsNullOrEmpty(eventName))
            query = query.Where(e => e.EventName == eventName);

        var timeseries = interval.ToLower() switch
        {
            "hour" => await query
                .GroupBy(e => new { e.Timestamp.Year, e.Timestamp.Month, e.Timestamp.Day, e.Timestamp.Hour })
                .Select(g => new
                {
                    timestamp = new DateTime(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, 0, 0),
                    count = g.Count()
                })
                .OrderBy(x => x.timestamp)
                .ToListAsync(cancellationToken),
            "month" => await query
                .GroupBy(e => new { e.Timestamp.Year, e.Timestamp.Month })
                .Select(g => new
                {
                    timestamp = new DateTime(g.Key.Year, g.Key.Month, 1),
                    count = g.Count()
                })
                .OrderBy(x => x.timestamp)
                .ToListAsync(cancellationToken),
            _ => await query
                .GroupBy(e => new { e.Timestamp.Year, e.Timestamp.Month, e.Timestamp.Day })
                .Select(g => new
                {
                    timestamp = new DateTime(g.Key.Year, g.Key.Month, g.Key.Day),
                    count = g.Count()
                })
                .OrderBy(x => x.timestamp)
                .ToListAsync(cancellationToken)
        };

        return Ok(timeseries);
    }

    [HttpGet("users/timeseries")]
    public async Task<IActionResult> GetUsersTimeseries(
        Guid projectId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] string interval = "day",
        CancellationToken cancellationToken = default)
    {
        if (!IsValidInterval(interval))
            return BadRequest(new { error = $"Invalid interval '{interval}'. Allowed values: hour, day, month." });

        var query = dbContext.ProcessedEvents.AsNoTracking()
            .Where(e => e.ProjectId == projectId && e.Timestamp >= from && e.Timestamp <= to);

        var timeseries = interval.ToLower() switch
        {
            "hour" => await query
                .GroupBy(e => new { e.Timestamp.Year, e.Timestamp.Month, e.Timestamp.Day, e.Timestamp.Hour })
                .Select(g => new
                {
                    timestamp = new DateTime(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, 0, 0),
                    count = g.Select(x => x.UserId).Distinct().Count()
                })
                .OrderBy(x => x.timestamp)
                .ToListAsync(cancellationToken),
            "month" => await query
                .GroupBy(e => new { e.Timestamp.Year, e.Timestamp.Month })
                .Select(g => new
                {
                    timestamp = new DateTime(g.Key.Year, g.Key.Month, 1),
                    count = g.Select(x => x.UserId).Distinct().Count()
                })
                .OrderBy(x => x.timestamp)
                .ToListAsync(cancellationToken),
            _ => await query
                .GroupBy(e => new { e.Timestamp.Year, e.Timestamp.Month, e.Timestamp.Day })
                .Select(g => new
                {
                    timestamp = new DateTime(g.Key.Year, g.Key.Month, g.Key.Day),
                    count = g.Select(x => x.UserId).Distinct().Count()
                })
                .OrderBy(x => x.timestamp)
                .ToListAsync(cancellationToken)
        };

        return Ok(timeseries);
    }

    private static bool IsValidInterval(string interval) =>
        interval.ToLower() is "hour" or "day" or "month";
}
