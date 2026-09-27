namespace Govern.Domain.EntityBase;

/// <summary>
/// An entity with creation and last-update timestamps (UTC). The timestamps are maintained by the persistence
/// layer on save; domain code must not set them.
/// </summary>
public abstract class BaseAuditableEntity : BaseEntity
{
    /// <summary>
    /// When the entity was first saved.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When the entity was last saved.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
