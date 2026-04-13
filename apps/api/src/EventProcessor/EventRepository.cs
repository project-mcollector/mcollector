using Contracts.Messages;
using EventProcessor.Contracts;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventProcessor;

public class EventRepository(EventProcessorDbContext dbContext) : IProcessedEventRepository
{
    public Task<ProcessedEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.ProcessedEvents.FirstOrDefaultAsync(e => e.EventId == id, cancellationToken);
    }

    public async Task AddAsync(ProcessedEvent entity, CancellationToken cancellationToken = default)
    {
        await dbContext.ProcessedEvents.AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ProcessedEvent entity, CancellationToken cancellationToken = default)
    {
        dbContext.ProcessedEvents.Update(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(ProcessedEvent entity, CancellationToken cancellationToken = default)
    {
        dbContext.ProcessedEvents.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> ExistsByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return dbContext.ProcessedEvents.AnyAsync(e => e.EventId == eventId, cancellationToken);
    }
}
