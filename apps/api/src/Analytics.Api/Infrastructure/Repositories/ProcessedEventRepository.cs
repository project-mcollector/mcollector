using Contracts.Messages;
using Infrastructure.Persistence;

namespace Analytics.Api.Infrastructure.Repositories;

public class ProcessedEventRepository(Persistence.AnalyticsDbContext context) : IRepository<ProcessedEvent, Guid>
{
    public async Task<ProcessedEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await context.ProcessedEvents.FindAsync([id], cancellationToken);

    public async Task AddAsync(ProcessedEvent entity, CancellationToken cancellationToken = default)
    {
        await context.ProcessedEvents.AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ProcessedEvent entity, CancellationToken cancellationToken = default)
    {
        context.ProcessedEvents.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(ProcessedEvent entity, CancellationToken cancellationToken = default)
    {
        context.ProcessedEvents.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
