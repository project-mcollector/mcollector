using Contracts.Messages;
using Infrastructure.Auth;
using Ingestion.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ingestion.Api.Controllers;

[ApiController]
[Route("api/v1/ingest")]
[Authorize]
public class IngestionController(IIngestionService ingestionService, IApiKeyValidator apiKeyValidator) : ControllerBase
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
        [FromBody] List<IngestEventRequest> requests,
        CancellationToken cancellationToken)
    {
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

        var rawEvents = request.Events.Select(e => BuildRawEvent(
            projectId.Value, e.Event,
            e.UserId, e.AnonymousId, e.SessionId,
            e.Properties, e.Timestamp ?? DateTimeOffset.UtcNow,
            e.UserAgent));

        await ingestionService.IngestBatchAsync(rawEvents, cancellationToken);
        return Accepted();
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