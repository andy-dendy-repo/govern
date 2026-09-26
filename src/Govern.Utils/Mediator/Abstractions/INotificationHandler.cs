namespace Govern.Utils.Mediator.Abstractions;

/// <summary>
/// Reacts to a published notification.
/// </summary>
/// <typeparam name="TNotification">The handled notification type.</typeparam>
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Handles <paramref name="notification"/>.
    /// </summary>
    /// <param name="notification">The published notification.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>A task that completes when the notification is handled.</returns>
    ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken);
}
