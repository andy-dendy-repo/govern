namespace Govern.Domain.EntityBase;

/// <summary>
/// Base for all entities: a time-ordered Guid v7 identifier and the domain events raised since the last save.
/// Events are dispatched after <c>SaveChanges</c> by the persistence layer and then cleared.
/// </summary>
public abstract class BaseEntity
{
    private readonly List<BaseEvent> _domainEvents = [];

    /// <summary>
    /// The identifier, generated as a Guid v7 so inserts stay index-friendly.
    /// </summary>
    public Guid Id { get; protected init; } = Guid.CreateVersion7();

    /// <summary>
    /// Domain events raised by this entity that have not been dispatched yet.
    /// </summary>
    public IReadOnlyCollection<BaseEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Removes all pending domain events (called by the persistence layer once they are dispatched).
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    /// Records a domain event to be dispatched after the entity is saved.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    protected void AddDomainEvent(BaseEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }
}
