using Contracts.Messages;
using Ingestion.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ingestion.Api.Controllers;

[ApiController]
[Route("api/v1/ingest")]
public class IngestionController(IIngestionService ingestionService) : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok" });

    [HttpPost("event")]
    public async Task<IActionResult> IngestEvent(
        [FromHeader(Name = "X-Project-Id")] Guid projectId,
        [FromHeader(Name = "X-Api-Key")] string apiKey,
        [FromBody] IngestEventRequest request,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
            return BadRequest(new { error = "X-Project-Id is required" });

        if (string.IsNullOrWhiteSpace(apiKey))
            return BadRequest(new { error = "X-Api-Key is required" });

        if (string.IsNullOrWhiteSpace(request.EventName))
            return BadRequest(new { error = "eventName is required" });

        if (string.IsNullOrWhiteSpace(request.UserId) && string.IsNullOrWhiteSpace(request.AnonymousId))
            return BadRequest(new { error = "userId or anonymousId is required" });

        var rawEvent = new RawEvent
        {
            ProjectId       = projectId,
            EventName       = request.EventName,
            UserId          = request.UserId,
            AnonymousId     = request.AnonymousId,
            SessionId       = request.SessionId,
            Properties      = request.Properties,
            ClientTimestamp = request.ClientTimestamp,
            ServerTimestamp = DateTimeOffset.UtcNow,
            IpAddress       = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent       = Request.Headers.UserAgent.ToString()
        };

        await ingestionService.IngestAsync(rawEvent, cancellationToken);
        return Accepted();
    }

    [HttpPost("batch")]
    public async Task<IActionResult> IngestBatch(
        [FromHeader(Name = "X-Project-Id")] Guid projectId,
        [FromHeader(Name = "X-Api-Key")] string apiKey,
        [FromBody] List<IngestEventRequest> requests,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
            return BadRequest(new { error = "X-Project-Id is required" });

        if (string.IsNullOrWhiteSpace(apiKey))
            return BadRequest(new { error = "X-Api-Key is required" });

        if (requests.Count > 50)
            return BadRequest(new { error = "Batch size cannot exceed 50 events" });

        var rawEvents = requests.Select(r => new RawEvent
        {
            ProjectId       = projectId,
            EventName       = r.EventName,
            UserId          = r.UserId,
            AnonymousId     = r.AnonymousId,
            SessionId       = r.SessionId,
            Properties      = r.Properties,
            ClientTimestamp = r.ClientTimestamp,
            ServerTimestamp = DateTimeOffset.UtcNow,
            IpAddress       = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent       = Request.Headers.UserAgent.ToString()
        });

        await ingestionService.IngestBatchAsync(rawEvents, cancellationToken);
        return Accepted();
    }
}
