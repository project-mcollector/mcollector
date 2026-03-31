namespace Contracts;

/// <summary>
/// Репозиторий для управления профилями пользователей и логикой их слияния.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Находит пользователя по одному из его анонимных идентификаторов.
    /// </summary>
    Task<User?> GetByAnonymousIdAsync(string anonymousId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Находит пользователя по его постоянному идентификатору.
    /// </summary>
    Task<User?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет нового пользователя в хранилище.
    /// </summary>
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет существующего пользователя в хранилище.
    /// </summary>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
}

// ВРЕМЕННАЯ ЗАГЛУШКА
public class User
{
}
