namespace Contracts;

public interface IEventRepository
{
    /// <summary>
    /// Добавляет событие в хранилище
    /// </summary>
    public Task AddAsync(Event newEvent);

    /// <summary>
    /// Добавляет пачку событий в хранилище
    /// </summary>
    public Task AddBatchAsync(IEnumerable<Event> events);

    /// <summary>
    /// Находит все события по анонимному ID и присваивает им реальный ID пользователя.
    /// </summary>
    /// <returns>Количество обновленных событий.</returns>
    public Task<int> UpdateUserIdAsync(string anonymousId, string newUserId);
}

// ВРЕМЕННАЯ ЗАГЛУШКА
// Позже, когда мы закончим с интерфейсами, мы заменим
// эту заглушку на полноценный класс-сущность.
public class Event { }
