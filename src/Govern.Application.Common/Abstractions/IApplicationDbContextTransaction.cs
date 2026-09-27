namespace Govern.Application.Common.Abstractions;

/// <summary>
/// An explicit database transaction. Disposing it without <see cref="CommitAsync"/> rolls it back.
/// </summary>
public interface IApplicationDbContextTransaction : IAsyncDisposable
{
    /// <summary>Commits the transaction.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>A task that completes when committed.</returns>
    Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>Rolls the transaction back.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>A task that completes when rolled back.</returns>
    Task RollbackAsync(CancellationToken cancellationToken);
}
