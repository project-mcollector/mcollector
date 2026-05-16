using Contracts.Messages;
using Infrastructure.Auth;
using Ingestion.Api.Controllers;
using Ingestion.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Ingestion.Api.Tests;

/// <summary>
/// Additional unit tests covering gaps in IngestionController:
/// - IngestEvent edge cases (AnonymousId-only)
/// - IngestBatch boundary and per-item validation
/// - SdkBatch (the entire anonymous writeKey endpoint)
/// </summary>
public class IngestionControllerAdditionalTests
{
    // Helper that returns both the controller and the mocks so callers can
    // set up expectations on the validator mock.
    private static (IngestionController Controller, Mock<IIngestionService> Service, Mock<IApiKeyValidator> Validator)
        CreateController()
    {
        var mockService = new Mock<IIngestionService>();
        var mockValidator = new Mock<IApiKeyValidator>();
        var controller = new IngestionController(
            mockService.Object,
            mockValidator.Object,
            NullLogger<IngestionController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return (controller, mockService, mockValidator);
    }

    // ───────────────────────────── IngestEvent ─────────────────────────────

    [Fact]
    public async Task IngestEvent_AnonymousIdOnly_ReturnsAccepted()
    {
        var (controller, mockService, _) = CreateController();
        var projectId = Guid.NewGuid();

        var request = new IngestEventRequest
        {
            EventName = "page_view",
            AnonymousId = "anon-abc",
            ClientTimestamp = DateTimeOffset.UtcNow
        };

        var result = await controller.IngestEvent(projectId, request, CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
        mockService.Verify(x => x.IngestAsync(
            It.Is<RawEvent>(e => e.ProjectId == projectId && e.AnonymousId == "anon-abc"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestEvent_AnonymousIdOnly_RawEventUserIdFallsBackToAnonymousId()
    {
        var (controller, mockService, _) = CreateController();
        var projectId = Guid.NewGuid();

        var request = new IngestEventRequest
        {
            EventName = "click",
            AnonymousId = "anon-xyz",
            ClientTimestamp = DateTimeOffset.UtcNow
        };

        await controller.IngestEvent(projectId, request, CancellationToken.None);

        mockService.Verify(x => x.IngestAsync(
            It.Is<RawEvent>(e => e.UserId == "anon-xyz"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestEvent_WhitespaceEventName_ReturnsBadRequest()
    {
        var (controller, mockService, _) = CreateController();

        var request = new IngestEventRequest
        {
            EventName = "   ",
            UserId = "user1",
            ClientTimestamp = DateTimeOffset.UtcNow
        };

        var result = await controller.IngestEvent(Guid.NewGuid(), request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        mockService.Verify(x => x.IngestAsync(It.IsAny<RawEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ───────────────────────────── IngestBatch ─────────────────────────────

    [Fact]
    public async Task IngestBatch_NullBody_ReturnsBadRequest()
    {
        var (controller, mockService, _) = CreateController();

        var result = await controller.IngestBatch(Guid.NewGuid(), null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        mockService.Verify(x => x.IngestBatchAsync(It.IsAny<IEnumerable<RawEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestBatch_EmptyList_ReturnsBadRequest()
    {
        var (controller, mockService, _) = CreateController();

        var result = await controller.IngestBatch(Guid.NewGuid(), new List<IngestEventRequest>(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        mockService.Verify(x => x.IngestBatchAsync(It.IsAny<IEnumerable<RawEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestBatch_Exactly50Events_ReturnsAccepted()
    {
        var (controller, mockService, _) = CreateController();

        var requests = Enumerable.Range(0, 50)
            .Select(_ => new IngestEventRequest
            {
                EventName = "event",
                UserId = "user",
                ClientTimestamp = DateTimeOffset.UtcNow
            }).ToList();

        var result = await controller.IngestBatch(Guid.NewGuid(), requests, CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
        mockService.Verify(x => x.IngestBatchAsync(It.IsAny<IEnumerable<RawEvent>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestBatch_ItemMissingEventName_ReturnsBadRequest()
    {
        var (controller, mockService, _) = CreateController();

        var requests = new List<IngestEventRequest>
        {
            new() { EventName = "ok", UserId = "user1", ClientTimestamp = DateTimeOffset.UtcNow },
            new() { EventName = "", UserId = "user2", ClientTimestamp = DateTimeOffset.UtcNow }
        };

        var result = await controller.IngestBatch(Guid.NewGuid(), requests, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        mockService.Verify(x => x.IngestBatchAsync(It.IsAny<IEnumerable<RawEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestBatch_ItemMissingBothUserIds_ReturnsBadRequest()
    {
        var (controller, mockService, _) = CreateController();

        var requests = new List<IngestEventRequest>
        {
            new() { EventName = "ok", UserId = "user1", ClientTimestamp = DateTimeOffset.UtcNow },
            new() { EventName = "no-id", UserId = null, AnonymousId = null, ClientTimestamp = DateTimeOffset.UtcNow }
        };

        var result = await controller.IngestBatch(Guid.NewGuid(), requests, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        mockService.Verify(x => x.IngestBatchAsync(It.IsAny<IEnumerable<RawEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ───────────────────────────── SdkBatch ────────────────────────────────

    [Fact]
    public async Task SdkBatch_MissingWriteKey_ReturnsBadRequest()
    {
        var (controller, mockService, _) = CreateController();

        var request = new SdkBatchRequest
        {
            WriteKey = "",
            Events = new List<SdkEventRequest>
            {
                new() { Event = "click", UserId = "u1" }
            }
        };

        var result = await controller.SdkBatch(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        mockService.Verify(x => x.IngestBatchAsync(It.IsAny<IEnumerable<RawEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SdkBatch_WhitespaceWriteKey_ReturnsBadRequest()
    {
        var (controller, mockService, _) = CreateController();

        var request = new SdkBatchRequest
        {
            WriteKey = "   ",
            Events = new List<SdkEventRequest>
            {
                new() { Event = "click", UserId = "u1" }
            }
        };

        var result = await controller.SdkBatch(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SdkBatch_EmptyEventsList_ReturnsBadRequest()
    {
        var (controller, mockService, _) = CreateController();

        var request = new SdkBatchRequest
        {
            WriteKey = "valid-key",
            Events = new List<SdkEventRequest>()
        };

        var result = await controller.SdkBatch(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        mockService.Verify(x => x.IngestBatchAsync(It.IsAny<IEnumerable<RawEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SdkBatch_TooManyEvents_ReturnsBadRequest()
    {
        var (controller, mockService, _) = CreateController();

        var request = new SdkBatchRequest
        {
            WriteKey = "valid-key",
            Events = Enumerable.Range(0, 51)
                .Select(_ => new SdkEventRequest { Event = "e", UserId = "u" })
                .ToList()
        };

        var result = await controller.SdkBatch(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        mockService.Verify(x => x.IngestBatchAsync(It.IsAny<IEnumerable<RawEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SdkBatch_InvalidWriteKey_ReturnsUnauthorized()
    {
        var (controller, mockService, mockValidator) = CreateController();

        mockValidator
            .Setup(v => v.GetProjectIdByApiKeyAsync("bad-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var request = new SdkBatchRequest
        {
            WriteKey = "bad-key",
            Events = new List<SdkEventRequest>
            {
                new() { Event = "click", UserId = "u1" }
            }
        };

        var result = await controller.SdkBatch(request, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        mockService.Verify(x => x.IngestBatchAsync(It.IsAny<IEnumerable<RawEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SdkBatch_ValidWriteKey_ReturnsAcceptedAndCallsService()
    {
        var (controller, mockService, mockValidator) = CreateController();
        var projectId = Guid.NewGuid();

        mockValidator
            .Setup(v => v.GetProjectIdByApiKeyAsync("good-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(projectId);

        var request = new SdkBatchRequest
        {
            WriteKey = "good-key",
            Events = new List<SdkEventRequest>
            {
                new() { Event = "page_view", UserId = "user1" },
                new() { Event = "click", AnonymousId = "anon1" }
            }
        };

        var result = await controller.SdkBatch(request, CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
        mockService.Verify(x => x.IngestBatchAsync(
            It.Is<IEnumerable<RawEvent>>(events =>
                events.All(e => e.ProjectId == projectId)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SdkBatch_ContextMergesUrlAndReferrer()
    {
        var (controller, mockService, mockValidator) = CreateController();
        var projectId = Guid.NewGuid();

        mockValidator
            .Setup(v => v.GetProjectIdByApiKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(projectId);

        IEnumerable<RawEvent>? capturedEvents = null;
        mockService
            .Setup(s => s.IngestBatchAsync(It.IsAny<IEnumerable<RawEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<RawEvent>, CancellationToken>((events, _) => capturedEvents = events.ToList());

        var request = new SdkBatchRequest
        {
            WriteKey = "key",
            Events = new List<SdkEventRequest>
            {
                new()
                {
                    Event = "page_view",
                    UserId = "u1",
                    Context = new SdkEventContext
                    {
                        Url = "https://example.com",
                        Referrer = "https://google.com"
                    }
                }
            }
        };

        await controller.SdkBatch(request, CancellationToken.None);

        Assert.NotNull(capturedEvents);
        var ev = capturedEvents.Single();
        Assert.NotNull(ev.Properties);
        Assert.Equal("https://example.com", ev.Properties["$url"].ToString());
        Assert.Equal("https://google.com", ev.Properties["$referrer"].ToString());
    }

    [Fact]
    public async Task SdkBatch_ContextMergesUtmParameters()
    {
        var (controller, mockService, mockValidator) = CreateController();
        var projectId = Guid.NewGuid();

        mockValidator
            .Setup(v => v.GetProjectIdByApiKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(projectId);

        IEnumerable<RawEvent>? capturedEvents = null;
        mockService
            .Setup(s => s.IngestBatchAsync(It.IsAny<IEnumerable<RawEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<RawEvent>, CancellationToken>((events, _) => capturedEvents = events.ToList());

        var request = new SdkBatchRequest
        {
            WriteKey = "key",
            Events = new List<SdkEventRequest>
            {
                new()
                {
                    Event = "page_view",
                    UserId = "u1",
                    Context = new SdkEventContext
                    {
                        Utm = new SdkEventUtm
                        {
                            Source = "google",
                            Medium = "cpc",
                            Campaign = "summer_sale"
                        }
                    }
                }
            }
        };

        await controller.SdkBatch(request, CancellationToken.None);

        Assert.NotNull(capturedEvents);
        var ev = capturedEvents.Single();
        Assert.NotNull(ev.Properties);
        Assert.Equal("google", ev.Properties["$utm_source"].ToString());
        Assert.Equal("cpc", ev.Properties["$utm_medium"].ToString());
        Assert.Equal("summer_sale", ev.Properties["$utm_campaign"].ToString());
    }

    [Fact]
    public async Task SdkBatch_ContextUserAgentOverridesHeader()
    {
        var (controller, mockService, mockValidator) = CreateController();
        var projectId = Guid.NewGuid();

        mockValidator
            .Setup(v => v.GetProjectIdByApiKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(projectId);

        // Simulate request with a different header-level user agent
        controller.ControllerContext.HttpContext.Request.Headers.UserAgent = "HeaderUA";

        IEnumerable<RawEvent>? capturedEvents = null;
        mockService
            .Setup(s => s.IngestBatchAsync(It.IsAny<IEnumerable<RawEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<RawEvent>, CancellationToken>((events, _) => capturedEvents = events.ToList());

        var request = new SdkBatchRequest
        {
            WriteKey = "key",
            Events = new List<SdkEventRequest>
            {
                new()
                {
                    Event = "page_view",
                    UserId = "u1",
                    Context = new SdkEventContext { UserAgent = "SDK-Browser/1.0" }
                }
            }
        };

        await controller.SdkBatch(request, CancellationToken.None);

        Assert.NotNull(capturedEvents);
        var ev = capturedEvents.Single();
        Assert.Equal("SDK-Browser/1.0", ev.UserAgent);
    }

    [Fact]
    public async Task SdkBatch_NullTimestamp_FallsBackToUtcNow()
    {
        var (controller, mockService, mockValidator) = CreateController();
        var projectId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;

        mockValidator
            .Setup(v => v.GetProjectIdByApiKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(projectId);

        IEnumerable<RawEvent>? capturedEvents = null;
        mockService
            .Setup(s => s.IngestBatchAsync(It.IsAny<IEnumerable<RawEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<RawEvent>, CancellationToken>((events, _) => capturedEvents = events.ToList());

        var request = new SdkBatchRequest
        {
            WriteKey = "key",
            Events = new List<SdkEventRequest>
            {
                new() { Event = "page_view", UserId = "u1", Timestamp = null }
            }
        };

        await controller.SdkBatch(request, CancellationToken.None);

        var after = DateTimeOffset.UtcNow;
        Assert.NotNull(capturedEvents);
        var ev = capturedEvents.Single();
        Assert.InRange(ev.ClientTimestamp, before, after);
    }

    [Fact]
    public async Task SdkBatch_ContextMergesScreen()
    {
        var (controller, mockService, mockValidator) = CreateController();
        var projectId = Guid.NewGuid();

        mockValidator
            .Setup(v => v.GetProjectIdByApiKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(projectId);

        IEnumerable<RawEvent>? capturedEvents = null;
        mockService
            .Setup(s => s.IngestBatchAsync(It.IsAny<IEnumerable<RawEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<RawEvent>, CancellationToken>((events, _) => capturedEvents = events.ToList());

        var request = new SdkBatchRequest
        {
            WriteKey = "key",
            Events = new List<SdkEventRequest>
            {
                new()
                {
                    Event = "page_view",
                    UserId = "u1",
                    Context = new SdkEventContext
                    {
                        Screen = new SdkEventScreen { Width = 1920, Height = 1080 }
                    }
                }
            }
        };

        await controller.SdkBatch(request, CancellationToken.None);

        Assert.NotNull(capturedEvents);
        var ev = capturedEvents.Single();
        Assert.NotNull(ev.Properties);
        Assert.True(ev.Properties.ContainsKey("$screen"));
    }

    [Fact]
    public async Task SdkBatch_ExistingPropertiesMergedWithContext()
    {
        var (controller, mockService, mockValidator) = CreateController();
        var projectId = Guid.NewGuid();

        mockValidator
            .Setup(v => v.GetProjectIdByApiKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(projectId);

        IEnumerable<RawEvent>? capturedEvents = null;
        mockService
            .Setup(s => s.IngestBatchAsync(It.IsAny<IEnumerable<RawEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<RawEvent>, CancellationToken>((events, _) => capturedEvents = events.ToList());

        var request = new SdkBatchRequest
        {
            WriteKey = "key",
            Events = new List<SdkEventRequest>
            {
                new()
                {
                    Event = "page_view",
                    UserId = "u1",
                    Properties = new Dictionary<string, object> { ["custom_prop"] = "my_value" },
                    Context = new SdkEventContext { Url = "https://example.com" }
                }
            }
        };

        await controller.SdkBatch(request, CancellationToken.None);

        Assert.NotNull(capturedEvents);
        var ev = capturedEvents.Single();
        Assert.NotNull(ev.Properties);
        Assert.Equal("my_value", ev.Properties["custom_prop"].ToString());
        Assert.Equal("https://example.com", ev.Properties["$url"].ToString());
    }

    [Fact]
    public async Task SdkBatch_Exactly50Events_ReturnsAccepted()
    {
        var (controller, mockService, mockValidator) = CreateController();
        var projectId = Guid.NewGuid();

        mockValidator
            .Setup(v => v.GetProjectIdByApiKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(projectId);

        var request = new SdkBatchRequest
        {
            WriteKey = "key",
            Events = Enumerable.Range(0, 50)
                .Select(_ => new SdkEventRequest { Event = "e", UserId = "u" })
                .ToList()
        };

        var result = await controller.SdkBatch(request, CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
        mockService.Verify(x => x.IngestBatchAsync(It.IsAny<IEnumerable<RawEvent>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
