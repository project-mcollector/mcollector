namespace Contracts;

public interface IEventRepository
{
    /// <summary>
    /// Adds an event to the repository.
    /// </summary>
    public Task AddAsync(Event newEvent);

    /// <summary>
    /// Adds a batch of events to the repository.
    /// </summary>
    public Task AddBatchAsync(IEnumerable<Event> events);

    /// <summary>
    /// Finds all events by anonymous ID and assigns them a real user ID.
    /// </summary>
    /// <returns>The number of updated events.</returns>
    public Task<int> UpdateUserIdAsync(string anonymousId, string newUserId);
}

// TEMPORARY STUB
// Later, when we are done with interfaces, we will replace
// this stub with a full-fledged entity class.
public class Event { }
