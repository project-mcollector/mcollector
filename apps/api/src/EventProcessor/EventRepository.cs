using Contracts.Messages;
using EventProcessor.Contracts;
using Infrastructure.Persistence;

namespace EventProcessor;

public class EventRepository : IProcessedEventRepository
{
    public Task<ProcessedEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task AddAsync(ProcessedEvent entity, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task UpdateAsync(ProcessedEvent entity, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(ProcessedEvent entity, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> ExistsByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
