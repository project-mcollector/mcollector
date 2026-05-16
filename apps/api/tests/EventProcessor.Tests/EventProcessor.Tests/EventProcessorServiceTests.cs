using System.Data.Common;
using System.Text.Json;
using Contracts.Messages;
using EventProcessor;
using EventProcessor.Contracts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EventProcessor.Tests;

/// <summary>
/// Minimal DbException subclass so we can satisfy the abstract-class constraint when
/// simulating unique-violation errors from the database layer.
/// </summary>
file sealed class FakeDbException(string message) : DbException(message);

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

    // -------------------------------------------------------------------------
    // Validation: invalid events are dropped without touching the repository
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConsumeAsync_EventWithNullEventName_ShouldNotSave()
    {
        // EventName is [Required] — a null value must be rejected by the validator.
        var invalidEvent = new RawEvent
        {
            EventId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            EventName = null!,
            UserId = "user-123"
        };

        await _service.ConsumeAsync(invalidEvent, CancellationToken.None);

        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<ProcessedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ConsumeAsync_EventWithNullUserId_ShouldNotSave()
    {
        // UserId is [Required].
        var invalidEvent = new RawEvent
        {
            EventId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            EventName = "page_view",
            UserId = null!
        };

        await _service.ConsumeAsync(invalidEvent, CancellationToken.None);

        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<ProcessedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // -------------------------------------------------------------------------
    // Happy path: valid event is persisted
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConsumeAsync_ValidEvent_ShouldCallAddAsync()
    {
        var validEvent = CreateValidRawEvent();

        await _service.ConsumeAsync(validEvent, CancellationToken.None);

        _repositoryMock.Verify(r => r.AddAsync(
            It.Is<ProcessedEvent>(pe =>
                pe.EventId == validEvent.EventId &&
                pe.EventName == validEvent.EventName &&
                pe.ProjectId == validEvent.ProjectId &&
                pe.UserId == validEvent.UserId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // -------------------------------------------------------------------------
    // Timestamp selection: ClientTimestamp preferred when non-default
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConsumeAsync_WithClientTimestamp_ShouldUseClientTimestamp()
    {
        var clientTs = new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var serverTs = new DateTimeOffset(2025, 6, 1, 10, 0, 5, TimeSpan.Zero);

        var rawEvent = CreateValidRawEvent() with
        {
            ClientTimestamp = clientTs,
            ServerTimestamp = serverTs
        };

        ProcessedEvent? captured = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ProcessedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessedEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        await _service.ConsumeAsync(rawEvent, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Timestamp.Should().Be(clientTs);
    }

    [Fact]
    public async Task ConsumeAsync_WithDefaultClientTimestamp_ShouldUseServerTimestamp()
    {
        var serverTs = new DateTimeOffset(2025, 6, 1, 10, 0, 5, TimeSpan.Zero);

        // ClientTimestamp left at default(DateTimeOffset) = DateTimeOffset.MinValue
        var rawEvent = CreateValidRawEvent() with
        {
            ClientTimestamp = default,
            ServerTimestamp = serverTs
        };

        ProcessedEvent? captured = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ProcessedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessedEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        await _service.ConsumeAsync(rawEvent, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Timestamp.Should().Be(serverTs);
    }

    // -------------------------------------------------------------------------
    // Properties serialization
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConsumeAsync_WithNullProperties_ShouldStoreNullPropertiesJson()
    {
        var rawEvent = CreateValidRawEvent() with { Properties = null };

        ProcessedEvent? captured = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ProcessedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessedEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        await _service.ConsumeAsync(rawEvent, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.PropertiesJson.Should().BeNull();
    }

    [Fact]
    public async Task ConsumeAsync_WithProperties_ShouldSerializePropertiesJson()
    {
        var props = new Dictionary<string, object> { ["key"] = "value", ["count"] = 42 };
        var rawEvent = CreateValidRawEvent() with { Properties = props };

        ProcessedEvent? captured = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ProcessedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessedEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        await _service.ConsumeAsync(rawEvent, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.PropertiesJson.Should().NotBeNull();
        // Round-trip to verify it's valid JSON representing the dictionary keys
        var deserialized = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(captured.PropertiesJson!);
        deserialized.Should().ContainKey("key");
        deserialized.Should().ContainKey("count");
    }

    // -------------------------------------------------------------------------
    // Duplicate detection: DbUpdateException with unique-violation inner exception
    // is swallowed silently; the event is not re-thrown.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConsumeAsync_DuplicateEvent_ShouldSwallowUniqueViolationException()
    {
        var validEvent = CreateValidRawEvent();

        var uniqueViolation = new DbUpdateException(
            "Conflict",
            new FakeDbException("duplicate key value violates unique constraint"));

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ProcessedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(uniqueViolation);

        // Should not throw — the service swallows unique-violation errors.
        Func<Task> act = () => _service.ConsumeAsync(validEvent, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ConsumeAsync_DuplicateEvent_ShouldNotPropagateException()
    {
        // "23505" is the Postgres error code for unique_violation
        var validEvent = CreateValidRawEvent();
        var uniqueViolation = new DbUpdateException(
            "Conflict",
            new FakeDbException("ERROR: 23505 duplicate"));

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ProcessedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(uniqueViolation);

        Func<Task> act = () => _service.ConsumeAsync(validEvent, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ConsumeAsync_NonUniqueDbUpdateException_ShouldRethrow()
    {
        // A DbUpdateException whose InnerException is a plain Exception (not DbException)
        // should NOT be caught — it must bubble up.
        var validEvent = CreateValidRawEvent();
        var unrelatedDbException = new DbUpdateException("Unrelated DB error", new InvalidOperationException("disk full"));

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ProcessedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(unrelatedDbException);

        Func<Task> act = () => _service.ConsumeAsync(validEvent, CancellationToken.None);
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    private static RawEvent CreateValidRawEvent() =>
        new RawEvent
        {
            EventId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            EventName = "user_signup",
            UserId = "user-123",
            ClientTimestamp = DateTimeOffset.UtcNow
        };
}
