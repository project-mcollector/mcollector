namespace Shared;

public abstract class Entity : IEquatable<Entity>
{
    public Guid Id { get; protected init; }

    protected Entity(Guid id) => Id = id;
    protected Entity() { }

    public static bool operator ==(Entity? left, Entity? right)
        => left is not null && right is not null && left.Equals(right);

    public static bool operator !=(Entity? left, Entity? right)
        => !(left == right);

    public bool Equals(Entity? other)
    {
        if (other is null || other.GetType() != GetType())
            return false;

        return Id == other.Id;
    }

    public override bool Equals(object? obj)
        => obj is Entity entity && Equals(entity);

    public override int GetHashCode()
        => Id.GetHashCode();
}
