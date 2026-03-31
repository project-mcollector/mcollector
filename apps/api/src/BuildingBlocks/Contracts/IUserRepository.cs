namespace Contracts;

/// <summary>
/// Repository for managing user profiles and their merging logic.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Finds a user by one of their anonymous identifiers.
    /// </summary>
    Task<User?> GetByAnonymousIdAsync(string anonymousId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a user by their permanent identifier.
    /// </summary>
    Task<User?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new user to the repository.
    /// </summary>
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user in the repository.
    /// </summary>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
}

// TEMPORARY STUB
public class User
{
}
