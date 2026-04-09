using Analytics.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Analytics.Api.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalyticsController(AnalyticsDbContext dbContext) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromQuery] Guid projectId, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to)
    {
        var query = dbContext.ProcessedEvents.AsNoTracking().Where(e => e.ProjectId == projectId);

        if (from.HasValue) query = query.Where(e => e.ProcessedAt >= from.Value);
        if (to.HasValue) query = query.Where(e => e.ProcessedAt <= to.Value);

        var totalEvents = await query.CountAsync();
        var uniqueUsers = await query.Select(e => e.UserId).Distinct().CountAsync();
        var pageViews = await query.Where(e => e.EventName == "page_view").CountAsync();

        return Ok(new { totalEvents, uniqueUsers, pageViews });
    }

    [HttpGet("events")]
    public async Task<IActionResult> GetEvents([FromQuery] Guid projectId)
    {
        var rawEvents = await dbContext.ProcessedEvents
            .AsNoTracking()
            .Where(e => e.ProjectId == projectId)
            .Select(e => e.EventName)
            .Distinct()
            .ToListAsync();

        return Ok(rawEvents);
    }

    [HttpGet("events/{eventName}/properties")]
    public async Task<IActionResult> GetEventProperties([FromQuery] Guid projectId, string eventName)
    {
        const int sampleSize = 100;

        var jsonStrings = await dbContext.ProcessedEvents
            .AsNoTracking()
            .Where(e => e.ProjectId == projectId && e.EventName == eventName && e.PropertiesJson != null)
            .OrderByDescending(e => e.ProcessedAt)
            .Select(e => e.PropertiesJson)
            .Take(sampleSize)
            .ToListAsync();

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
            catch
            {
                // ignore invalid json
            }
        }

        return Ok(properties);
    }

    [HttpGet("events/timeseries")]
    public async Task<IActionResult> GetEventsTimeseries(
        [FromQuery] Guid projectId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] string interval = "day",
        [FromQuery] string? eventName = null)
    {
        var query = dbContext.ProcessedEvents
            .AsNoTracking()
            .Where(e => e.ProjectId == projectId && e.ProcessedAt >= from && e.ProcessedAt <= to);

        if (!string.IsNullOrEmpty(eventName))
            query = query.Where(e => e.EventName == eventName);

        var data = await query.ToListAsync();
        Func<DateTimeOffset, DateTime> groupSelector = interval.ToLower() switch
        {
            "hour" => dt => new(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0),
            "month" => dt => new(dt.Year, dt.Month, 1),
            _ => dt => new(dt.Year, dt.Month, dt.Day)
        };

        var timeseries = data
            .GroupBy(e => groupSelector(e.ProcessedAt))
            .Select(g => new { timestamp = g.Key, count = g.Count() })
            .OrderBy(x => x.timestamp)
            .ToList();

        return Ok(timeseries);
    }

    [HttpGet("users/timeseries")]
    public async Task<IActionResult> GetUsersTimeseries(
        [FromQuery] Guid projectId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] string interval = "day")
    {
        var query = dbContext.ProcessedEvents.AsNoTracking()
            .Where(e => e.ProjectId == projectId && e.ProcessedAt >= from && e.ProcessedAt <= to);

        var data = await query.ToListAsync(); // Client eval for MVP grouping
        Func<DateTimeOffset, DateTime> groupSelector = interval.ToLower() switch
        {
            "hour" => dt => new(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0),
            "month" => dt => new(dt.Year, dt.Month, 1),
            _ => dt => new(dt.Year, dt.Month, dt.Day)
        };

        var timeseries = data
            .GroupBy(e => groupSelector(e.ProcessedAt))
            .Select(g => new { timestamp = g.Key, count = g.Select(x => x.UserId).Distinct().Count() })
            .OrderBy(x => x.timestamp)
            .ToList();

        return Ok(timeseries);
    }
}
