using Contracts;
using MediatR;

namespace Infrastructure;

public class MediatorEventBus(IMediator mediator) : IEventBus
{
    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default)
        where T : IIntegrationEvent
        => await mediator.Publish(@event ?? throw new ArgumentNullException(nameof(@event)), cancellationToken);
}
