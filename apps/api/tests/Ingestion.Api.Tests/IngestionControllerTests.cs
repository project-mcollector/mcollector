using Contracts.Messages;
using Ingestion.Api.Controllers;
using Ingestion.Api.Models;
using Ingestion.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Ingestion.Api.Tests;

public class IngestionControllerTests
{
    private static IngestionController CreateController(Mock<IIngestionService> mockService)
    {
        var controller = new IngestionController(mockService.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    [Fact]
    public async Task IngestEvent_ValidRequest_ReturnsAccepted()
    {
        // Arrange
        var mockService = new Mock<IIngestionService>();
        var controller = CreateController(mockService);

        var request = new IngestEventRequest
        {
            EventName = "test_event",
            UserId = "user1",
            ClientTimestamp = DateTimeOffset.UtcNow
        };

        var projectId = Guid.NewGuid();

        // Act
        var result = await controller.IngestEvent(
            projectId,
            "api-key",
            request,
            CancellationToken.None);

        // Assert
        Assert.IsType<AcceptedResult>(result);
        mockService.Verify(x => x.IngestAsync(It.IsAny<RawEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestEvent_MissingProjectId_ReturnsBadRequest()
    {
        var mockService = new Mock<IIngestionService>();
        var controller = CreateController(mockService);

        var request = new IngestEventRequest
        {
            EventName = "test",
            UserId = "user1",
            ClientTimestamp = DateTimeOffset.UtcNow
        };

        var result = await controller.IngestEvent(
            Guid.Empty,
            "api-key",
            request,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task IngestEvent_MissingApiKey_ReturnsBadRequest()
    {
        var mockService = new Mock<IIngestionService>();
        var controller = CreateController(mockService);

        var request = new IngestEventRequest
        {
            EventName = "test",
            UserId = "user1",
            ClientTimestamp = DateTimeOffset.UtcNow
        };

        var result = await controller.IngestEvent(
            Guid.NewGuid(),
            "",
            request,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task IngestEvent_MissingEventName_ReturnsBadRequest()
    {
        var mockService = new Mock<IIngestionService>();
        var controller = CreateController(mockService);

        var request = new IngestEventRequest
        {
            EventName = "",
            UserId = "user1",
            ClientTimestamp = DateTimeOffset.UtcNow
        };

        var result = await controller.IngestEvent(
            Guid.NewGuid(),
            "api-key",
            request,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task IngestEvent_NoUserIdAndAnonymousId_ReturnsBadRequest()
    {
        var mockService = new Mock<IIngestionService>();
        var controller = CreateController(mockService);

        var request = new IngestEventRequest
        {
            EventName = "test",
            UserId = "",
            AnonymousId = "",
            ClientTimestamp = DateTimeOffset.UtcNow
        };

        var result = await controller.IngestEvent(
            Guid.NewGuid(),
            "api-key",
            request,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task IngestBatch_ValidRequest_ReturnsAccepted()
    {
        var mockService = new Mock<IIngestionService>();
        var controller = CreateController(mockService);

        var requests = new List<IngestEventRequest>
        {
            new()
            {
                EventName = "event1",
                UserId = "user1",
                ClientTimestamp = DateTimeOffset.UtcNow
            },
            new()
            {
                EventName = "event2",
                UserId = "user2",
                ClientTimestamp = DateTimeOffset.UtcNow
            }
        };

        var result = await controller.IngestBatch(
            Guid.NewGuid(),
            "api-key",
            requests,
            CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);

        mockService.Verify(x =>
                x.IngestBatchAsync(It.IsAny<IEnumerable<RawEvent>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IngestBatch_TooManyEvents_ReturnsBadRequest()
    {
        var mockService = new Mock<IIngestionService>();
        var controller = CreateController(mockService);

        var requests = Enumerable.Range(0, 51)
            .Select(_ => new IngestEventRequest
            {
                EventName = "event",
                UserId = "user",
                ClientTimestamp = DateTimeOffset.UtcNow
            }).ToList();

        var result = await controller.IngestBatch(
            Guid.NewGuid(),
            "api-key",
            requests,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
