namespace Govern.Application.Common.Abstractions;

/// <summary>
/// Creates short-lived <see cref="IApplicationDbContext"/> instances, one per operation.
/// </summary>
public interface IApplicationDbContextFactory
{
    /// <summary>
    /// Creates a new database context; the caller owns and disposes it.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>A new context.</returns>
    Task<IApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken);
}
