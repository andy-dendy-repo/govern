namespace Govern.Utils.Mediator.Options;

/// <summary>
/// Configures the mediator.
/// </summary>
public sealed class MediatorOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the mediator should use the caller's (parent) DI scope.
    /// Not applied yet: <see cref="Services.MediatorService"/> does not read this option.
    /// </summary>
    public bool UseParentScope { get; set; }
}
