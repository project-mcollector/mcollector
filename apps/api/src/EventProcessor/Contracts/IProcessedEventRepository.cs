using Contracts.Messages;
using Infrastructure.Persistence;

namespace EventProcessor.Contracts;

public interface IProcessedEventRepository : IRepository<ProcessedEvent, Guid>
{
    Task<bool> ExistsByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);
}
