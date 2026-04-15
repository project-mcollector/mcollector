using System.ComponentModel.DataAnnotations;
using Contracts.Messages;
using EventProcessor;
using EventProcessor.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EventProcessor.Tests;

public class EventProcessorServiceTests
{
    private readonly Mock<ILogger<EventProcessorService>> _loggerMock;
    private readonly Mock<IProcessedEventRepository> _repositoryMock;
    private readonly EventProcessorService _service;

    public EventProcessorServiceTests()
    {
        _loggerMock = new Mock<ILogger<EventProcessorService>>();
        _repositoryMock = new Mock<IProcessedEventRepository>();
        _service = new EventProcessorService(_loggerMock.Object, _repositoryMock.Object);
    }

    [Fact]
    public async Task ConsumeAsync_InvalidEvent_ShouldNotSaveAndLogWarning()
    {
        // Arrange
        var invalidEvent = new RawEvent
        {
            // Set required fields to valid types to avoid CS9035, but we can make it invalid via other means
            EventId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            EventName = null!, // Intentionally invalid for validation
            UserId = null!     // Intentionally invalid for validation
        };

        // Act
        await _service.ConsumeAsync(invalidEvent, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<ProcessedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsumeAsync_DuplicateEvent_ShouldNotSaveAndLogInformation()
    {
        // Arrange
        var validEvent = CreateValidRawEvent();
        _repositoryMock.Setup(r => r.ExistsByEventIdAsync(validEvent.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Simulate duplication

        // Act
        await _service.ConsumeAsync(validEvent, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<ProcessedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsumeAsync_ValidAndNewEvent_ShouldSaveToRepository()
    {
        // Arrange
        var validEvent = CreateValidRawEvent();
        _repositoryMock.Setup(r => r.ExistsByEventIdAsync(validEvent.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _service.ConsumeAsync(validEvent, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.AddAsync(It.Is<ProcessedEvent>(pe =>
            pe.EventId == validEvent.EventId &&
            pe.EventName == validEvent.EventName &&
            pe.UserId == validEvent.UserId
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    private RawEvent CreateValidRawEvent()
    {
        return new RawEvent
        {
            EventId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            EventName = "user_signup",
            UserId = "user-123",
            ClientTimestamp = DateTimeOffset.UtcNow
        };
    }
}
