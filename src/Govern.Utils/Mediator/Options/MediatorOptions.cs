using Govern.Utils.Mediator.Abstractions;

namespace Govern.Utils.Mediator.Options;

/// <summary>
/// Configures the mediator (see <c>AddMediator</c>).
/// </summary>
public sealed class MediatorOptions
{
    private readonly List<Type> _pipelines = [];

    /// <summary>
    /// Gets or sets a value indicating whether handlers are resolved from the caller's (parent) DI scope.
    /// <see langword="true"/> (default) shares scoped services such as the current user with the caller;
    /// <see langword="false"/> resolves every request in a new, separate DI scope (created from the root provider).
    /// </summary>
    public bool UseParentScope { get; set; } = true;

    /// <summary>
    /// Open generic pipeline types in execution order; the first one is the outermost.
    /// </summary>
    public IReadOnlyList<Type> Pipelines => _pipelines;

    /// <summary>
    /// Appends an open generic pipeline, e.g. <c>typeof(LoggingPipeline&lt;,&gt;)</c>.
    /// </summary>
    /// <param name="openGenericPipeline">An open generic type implementing <see cref="IPipeline{TRequest, TResponse}"/>.</param>
    /// <returns>These options, for chaining.</returns>
    /// <exception cref="ArgumentException">The type is not an open generic <see cref="IPipeline{TRequest, TResponse}"/>.</exception>
    public MediatorOptions AddPipeline(Type openGenericPipeline)
    {
        ArgumentNullException.ThrowIfNull(openGenericPipeline);

        var isPipeline = openGenericPipeline.IsGenericTypeDefinition
            && openGenericPipeline.GetInterfaces().Any(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipeline<,>));

        if (!isPipeline)
        {
            throw new ArgumentException($"{openGenericPipeline} must be an open generic IPipeline<,> implementation.", nameof(openGenericPipeline));
        }

        _pipelines.Add(openGenericPipeline);
        return this;
    }
}
