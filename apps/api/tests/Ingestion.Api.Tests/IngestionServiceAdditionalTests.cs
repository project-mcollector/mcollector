using Contracts.Messages;
using Confluent.Kafka;
using Ingestion.Api.Services;
using Infrastructure.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Ingestion.Api.Tests;

/// <summary>
/// Additional unit tests for IngestionService covering:
/// - Exception propagation when publisher throws
/// - Empty batch does not call publisher
/// - CancellationToken is forwarded to publisher
/// </summary>
public class IngestionServiceAdditionalTests
{
    [Fact]
    public async Task IngestAsync_PublisherThrows_PropagatesException()
    {
        var mockPublisher = new Mock<IEventPublisher>();
        mockPublisher
            .Setup(p => p.PublishAsync(It.IsAny<RawEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Kafka unavailable"));

        var service = new IngestionService(mockPublisher.Object, NullLogger<IngestionService>.Instance);
        var rawEvent = new RawEvent { EventName = "click", UserId = "user1" };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.IngestAsync(rawEvent, CancellationToken.None));
    }

    [Fact]
    public async Task IngestBatchAsync_PublisherThrowsOnSecondEvent_PropagatesException()
    {
        var mockPublisher = new Mock<IEventPublisher>();
        var callCount = 0;
        mockPublisher
            .Setup(p => p.PublishAsync(It.IsAny<RawEvent>(), It.IsAny<CancellationToken>()))
            .Returns<RawEvent, CancellationToken>((_, _) =>
            {
                callCount++;
                if (callCount == 2)
                    throw new InvalidOperationException("Kafka publish failed on event 2");
                return Task.CompletedTask;
            });

        var service = new IngestionService(mockPublisher.Object, NullLogger<IngestionService>.Instance);
        var events = new List<RawEvent>
        {
            new() { EventName = "e1", UserId = "u1" },
            new() { EventName = "e2", UserId = "u2" }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.IngestBatchAsync(events, CancellationToken.None));

        // Publisher was called for the first event only
        mockPublisher.Verify(p =>
            p.PublishAsync(It.IsAny<RawEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task IngestBatchAsync_EmptyEnumerable_PublisherNotCalled()
    {
        var mockPublisher = new Mock<IEventPublisher>();
        var service = new IngestionService(mockPublisher.Object, NullLogger<IngestionService>.Instance);

        await service.IngestBatchAsync(Enumerable.Empty<RawEvent>(), CancellationToken.None);

        mockPublisher.Verify(p =>
            p.PublishAsync(It.IsAny<RawEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task IngestAsync_ForwardsCancellationToken()
    {
        var mockPublisher = new Mock<IEventPublisher>();
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        var service = new IngestionService(mockPublisher.Object, NullLogger<IngestionService>.Instance);
        var rawEvent = new RawEvent { EventName = "click", UserId = "user1" };

        await service.IngestAsync(rawEvent, token);

        mockPublisher.Verify(p =>
            p.PublishAsync(rawEvent, token),
            Times.Once);
    }

    [Fact]
    public async Task IngestBatchAsync_ForwardsCancellationTokenToEachPublish()
    {
        var mockPublisher = new Mock<IEventPublisher>();
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        var service = new IngestionService(mockPublisher.Object, NullLogger<IngestionService>.Instance);
        var events = new List<RawEvent>
        {
            new() { EventName = "e1", UserId = "u1" },
            new() { EventName = "e2", UserId = "u2" }
        };

        await service.IngestBatchAsync(events, token);

        mockPublisher.Verify(p =>
            p.PublishAsync(It.IsAny<RawEvent>(), token),
            Times.Exactly(2));
    }
}
