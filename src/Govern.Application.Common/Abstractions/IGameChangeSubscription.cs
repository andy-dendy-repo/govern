namespace Govern.Application.Common.Abstractions;

/// <summary>
/// One listener's view of the changes to one game (see <see cref="IGameChangeService.Subscribe"/>). Changes coalesce: any
/// number of changes since the last wait complete the next wait once.
/// </summary>
public interface IGameChangeSubscription : IDisposable
{
    /// <summary>Waits until the game has changed since the previous wait (or since subscribing).</summary>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>A task that completes on the next change.</returns>
    ValueTask WaitAsync(CancellationToken cancellationToken);
}
