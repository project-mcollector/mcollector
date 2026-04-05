namespace Infrastructure.Persistence;

public interface IRepository<T, in TKey> where T : class
{
    Task<T?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
}
