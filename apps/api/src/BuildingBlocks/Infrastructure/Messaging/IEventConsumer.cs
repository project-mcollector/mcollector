namespace Infrastructure.Messaging;

public interface IEventConsumer<in T> where T : class
{
    Task ConsumeAsync(T message, CancellationToken cancellationToken = default);
}
