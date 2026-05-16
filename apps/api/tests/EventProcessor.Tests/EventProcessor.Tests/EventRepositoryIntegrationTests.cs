using Contracts.Messages;
using EventProcessor;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventProcessor.Tests;

public class EventRepositoryIntegrationTests : IDisposable
{
    private readonly EventProcessorDbContext _dbContext;
    private readonly EventRepository _repository;

    public EventRepositoryIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<EventProcessorDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new EventProcessorDbContext(options);
        _repository = new EventRepository(_dbContext, NullLogger<EventRepository>.Instance);
    }

    // -------------------------------------------------------------------------
    // AddAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AddAsync_ShouldAddEventToDatabase()
    {
        var newEvent = new ProcessedEvent
        {
            EventId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            EventName = "page_view",
            UserId = "user_456",
            ProcessedAt = DateTimeOffset.UtcNow,
            Timestamp = DateTimeOffset.UtcNow
        };

        await _repository.AddAsync(newEvent);

        var savedEvent = await _dbContext.ProcessedEvents.FindAsync(newEvent.EventId);
        savedEvent.Should().NotBeNull();
        savedEvent!.EventName.Should().Be("page_view");
        savedEvent.UserId.Should().Be("user_456");
    }

    // -------------------------------------------------------------------------
    // GetByIdAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_ExistingId_ShouldReturnEvent()
    {
        var eventId = Guid.NewGuid();
        var storedEvent = new ProcessedEvent
        {
            EventId = eventId,
            ProjectId = Guid.NewGuid(),
            EventName = "button_click",
            UserId = "user_abc",
            ProcessedAt = DateTimeOffset.UtcNow,
            Timestamp = DateTimeOffset.UtcNow
        };
        await _dbContext.ProcessedEvents.AddAsync(storedEvent);
        await _dbContext.SaveChangesAsync();

        var result = await _repository.GetByIdAsync(eventId);

        result.Should().NotBeNull();
        result!.EventId.Should().Be(eventId);
        result.EventName.Should().Be("button_click");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ShouldReturnNull()
    {
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // ExistsByEventIdAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExistsByEventIdAsync_ShouldReturnTrueIfEventExists()
    {
        var existingEventId = Guid.NewGuid();
        var existingEvent = new ProcessedEvent
        {
            EventId = existingEventId,
            ProjectId = Guid.NewGuid(),
            EventName = "click",
            UserId = "user_789",
            ProcessedAt = DateTimeOffset.UtcNow,
            Timestamp = DateTimeOffset.UtcNow
        };
        await _dbContext.ProcessedEvents.AddAsync(existingEvent);
        await _dbContext.SaveChangesAsync();

        var result = await _repository.ExistsByEventIdAsync(existingEventId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByEventIdAsync_ShouldReturnFalseIfEventDoesNotExist()
    {
        var result = await _repository.ExistsByEventIdAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // UpdateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ShouldPersistChanges()
    {
        var eventId = Guid.NewGuid();
        var original = new ProcessedEvent
        {
            EventId = eventId,
            ProjectId = Guid.NewGuid(),
            EventName = "original_event",
            UserId = "user_upd",
            ProcessedAt = DateTimeOffset.UtcNow,
            Timestamp = DateTimeOffset.UtcNow
        };
        await _dbContext.ProcessedEvents.AddAsync(original);
        await _dbContext.SaveChangesAsync();

        // Detach to simulate a fresh fetch
        _dbContext.ChangeTracker.Clear();

        var updated = original with { EventBrowser = "Firefox" };
        await _repository.UpdateAsync(updated);

        _dbContext.ChangeTracker.Clear();
        var persisted = await _dbContext.ProcessedEvents.FindAsync(eventId);
        persisted.Should().NotBeNull();
        persisted!.EventBrowser.Should().Be("Firefox");
    }

    // -------------------------------------------------------------------------
    // DeleteAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_ShouldRemoveEventFromDatabase()
    {
        var eventId = Guid.NewGuid();
        var ev = new ProcessedEvent
        {
            EventId = eventId,
            ProjectId = Guid.NewGuid(),
            EventName = "delete_me",
            UserId = "user_del",
            ProcessedAt = DateTimeOffset.UtcNow,
            Timestamp = DateTimeOffset.UtcNow
        };
        await _dbContext.ProcessedEvents.AddAsync(ev);
        await _dbContext.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();

        // Re-fetch so the DbContext tracks the entity
        var tracked = await _dbContext.ProcessedEvents.FindAsync(eventId);
        tracked.Should().NotBeNull();

        await _repository.DeleteAsync(tracked!);

        var gone = await _dbContext.ProcessedEvents.FindAsync(eventId);
        gone.Should().BeNull();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}
