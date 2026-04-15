using Contracts.Messages;
using EventProcessor;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
        _repository = new EventRepository(_dbContext);
    }

    [Fact]
    public async Task AddAsync_ShouldAddEventToDatabase()
    {
        // Arrange
        var newEvent = new ProcessedEvent
        {
            EventId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            EventName = "page_view",
            UserId = "user_456",
            ProcessedAt = DateTimeOffset.UtcNow,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        await _repository.AddAsync(newEvent);

        // Assert
        var savedEvent = await _dbContext.ProcessedEvents.FindAsync(newEvent.EventId);
        savedEvent.Should().NotBeNull();
        savedEvent!.EventName.Should().Be("page_view");
        savedEvent.UserId.Should().Be("user_456");
    }

    [Fact]
    public async Task ExistsByEventIdAsync_ShouldReturnTrueIfEventExists()
    {
        // Arrange
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

        // Act
        var result = await _repository.ExistsByEventIdAsync(existingEventId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByEventIdAsync_ShouldReturnFalseIfEventDoesNotExist()
    {
        // Act
        var result = await _repository.ExistsByEventIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}

