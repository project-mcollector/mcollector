using Contracts.Messages;
using Infrastructure.Auth;
using Ingestion.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ingestion.Api.Controllers;

[ApiController]
[Route("api/v1/ingest")]
[Authorize]
public class IngestionController(
    IIngestionService ingestionService,
    IApiKeyValidator apiKeyValidator,
    ILogger<IngestionController> logger) : ControllerBase
{
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { status = "ok" });

    [HttpPost("event")]
    public async Task<IActionResult> IngestEvent(
        [FromHeader(Name = "X-Project-Id")] Guid projectId,
        [FromBody] IngestEventRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EventName))
            return BadRequest(new { error = "eventName is required" });

        if (string.IsNullOrWhiteSpace(request.UserId) && string.IsNullOrWhiteSpace(request.AnonymousId))
            return BadRequest(new { error = "userId or anonymousId is required" });

        var rawEvent = BuildRawEvent(projectId, request.EventName,
            request.UserId, request.AnonymousId, request.SessionId,
            request.Properties, request.ClientTimestamp);

        await ingestionService.IngestAsync(rawEvent, cancellationToken);
        return Accepted();
    }

    [HttpPost("batch")]
    public async Task<IActionResult> IngestBatch(
        [FromHeader(Name = "X-Project-Id")] Guid projectId,
        [FromBody] List<IngestEventRequest>? requests,
        CancellationToken cancellationToken)
    {
        if (requests is null || requests.Count == 0)
            return BadRequest(new { error = "Request body must contain a non-empty array of events" });

        if (requests.Count > 50)
            return BadRequest(new { error = "Batch size cannot exceed 50 events" });

        var rawEvents = requests.Select(r => BuildRawEvent(projectId, r.EventName,
            r.UserId, r.AnonymousId, r.SessionId, r.Properties, r.ClientTimestamp));

        await ingestionService.IngestBatchAsync(rawEvents, cancellationToken);
        return Accepted();
    }

    // SDK endpoint accepts the JS SDK's native payload shape
    // Auth is done via writeKey in the body; no JWT/ApiKey headers needed
    [HttpPost("events")]
    [AllowAnonymous]
    public async Task<IActionResult> SdkBatch([FromBody] SdkBatchRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.WriteKey))
            return BadRequest(new { error = "writeKey is required" });

        if (request.Events.Count > 50)
            return BadRequest(new { error = "Batch size cannot exceed 50 events" });

        var projectId = await apiKeyValidator.GetProjectIdByApiKeyAsync(request.WriteKey, cancellationToken);
        if (projectId is null)
            return Unauthorized(new { error = "Invalid writeKey" });

        logger.LogInformation("SDK batch: {Count} events for project {ProjectId}", request.Events.Count, projectId);

        var rawEvents = request.Events.Select(e =>
        {
            var props = MergeContext(e.Properties, e.Context);
            var userAgent = e.Context?.UserAgent;
            return BuildRawEvent(
                projectId.Value, e.Event,
                e.UserId, e.AnonymousId, e.SessionId,
                props, e.Timestamp ?? DateTimeOffset.UtcNow,
                userAgent);
        });

        await ingestionService.IngestBatchAsync(rawEvents, cancellationToken);
        return Accepted();
    }

    private static Dictionary<string, object>? MergeContext(
        Dictionary<string, object>? properties,
        SdkEventContext? context)
    {
        if (context is null) return properties;

        var merged = properties is not null
            ? new Dictionary<string, object>(properties)
            : new Dictionary<string, object>();

        if (context.Url is not null) merged["$url"] = context.Url;
        if (context.Referrer is not null) merged["$referrer"] = context.Referrer;
        if (context.Screen is not null)
            merged["$screen"] = new { width = context.Screen.Width, height = context.Screen.Height };
        if (context.Utm is not null)
        {
            if (context.Utm.Source is not null) merged["$utm_source"] = context.Utm.Source;
            if (context.Utm.Medium is not null) merged["$utm_medium"] = context.Utm.Medium;
            if (context.Utm.Campaign is not null) merged["$utm_campaign"] = context.Utm.Campaign;
        }

        return merged;
    }

    private RawEvent BuildRawEvent(
        Guid projectId, string? eventName,
        string? userId, string? anonymousId, string? sessionId,
        Dictionary<string, object>? properties, DateTimeOffset clientTimestamp,
        string? userAgentOverride = null) => new()
        {
            ProjectId = projectId,
            EventName = eventName ?? string.Empty,
            UserId = userId ?? anonymousId ?? "anonymous",
            AnonymousId = anonymousId,
            SessionId = sessionId,
            Properties = properties,
            ClientTimestamp = clientTimestamp,
            ServerTimestamp = DateTimeOffset.UtcNow,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = userAgentOverride ?? Request.Headers.UserAgent.ToString()
        };
}
