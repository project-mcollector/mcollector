namespace Shared;

public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> DomainEvents = [];

    protected AggregateRoot(Guid id) : base(id) { }
    protected AggregateRoot() { }

    public IReadOnlyCollection<IDomainEvent> GetDomainEvents()
        => DomainEvents.AsReadOnly();

    public void ClearDomainEvents()
        => DomainEvents.Clear();

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
        => DomainEvents.Add(domainEvent);
}
