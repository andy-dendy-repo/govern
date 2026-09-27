namespace Govern.Application.Common.Abstractions;

/// <summary>
/// Tells the players watching a game that it changed. A change carries no data: watchers read the game again, each with
/// their own fog of war, so nothing one player may not see passes through here.
/// </summary>
public interface IGameChangeService
{
    /// <summary>
    /// Signals every current subscription of <paramref name="gameId"/>. Never blocks; call it after the change is saved.
    /// </summary>
    /// <param name="gameId">The game that changed.</param>
    void Publish(Guid gameId);

    /// <summary>
    /// Starts listening for changes of <paramref name="gameId"/>. Subscribe <b>before</b> reading the game, so a change saved
    /// between the read and the subscription is not missed. Dispose the subscription to stop listening.
    /// </summary>
    /// <param name="gameId">The game to watch.</param>
    /// <returns>The subscription.</returns>
    IGameChangeSubscription Subscribe(Guid gameId);
}
