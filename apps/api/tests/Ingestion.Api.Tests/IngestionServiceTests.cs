using Contracts.Messages;
using Ingestion.Api.Services;
using Infrastructure.Messaging;
using Moq;

public class IngestionServiceTests
{
    [Fact]
    public async Task IngestAsync_CallsPublisher()
    {
        var mockPublisher = new Mock<IEventPublisher>();
        var service = new IngestionService(mockPublisher.Object);

        var rawEvent = new RawEvent
        {
            EventName = null,
            UserId = null
        };

        await service.IngestAsync(rawEvent);

        mockPublisher.Verify(x =>
                x.PublishAsync(rawEvent, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IngestBatchAsync_CallsPublisherForEachEvent()
    {
        var mockPublisher = new Mock<IEventPublisher>();
        var service = new IngestionService(mockPublisher.Object);

        var events = new List<RawEvent>
        {
            new()
            {
                EventName = null,
                UserId = null
            },
            new()
            {
                EventName = null,
                UserId = null
            },
            new()
            {
                EventName = null,
                UserId = null
            }
        };

        await service.IngestBatchAsync(events);

        mockPublisher.Verify(x =>
                x.PublishAsync(It.IsAny<RawEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }
}
