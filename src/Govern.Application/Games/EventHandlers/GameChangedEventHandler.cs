using Govern.Application.Common.Abstractions;
using Govern.Domain.Events;
using Govern.Utils.Mediator.Abstractions;

namespace Govern.Application.Games.EventHandlers;

/// <summary>
/// Wakes the live streams of a game after a change to it was saved (see <c>WatchGameContract</c>).
/// </summary>
internal sealed class GameChangedEventHandler(IGameChangeService gameChanges) : INotificationHandler<GameChangedEvent>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(GameChangedEvent notification, CancellationToken cancellationToken)
    {
        gameChanges.Publish(notification.GameId);
        return ValueTask.CompletedTask;
    }
}
