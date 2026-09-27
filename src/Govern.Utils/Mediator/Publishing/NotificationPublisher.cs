using Govern.Utils.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.Utils.Mediator.Publishing;

/// <summary>
/// Publishes a notification whose type is only known at runtime to the handlers of that exact type.
/// One instance per notification type is created and cached by the mediator, so reflection runs once per type.
/// </summary>
internal abstract class NotificationPublisher
{
    public static NotificationPublisher Create(Type notificationType) =>
        (NotificationPublisher)Activator.CreateInstance(typeof(TypedNotificationPublisher<>).MakeGenericType(notificationType))!;

    public abstract ValueTask PublishAsync(INotification notification, IServiceProvider provider, CancellationToken cancellationToken);

    private sealed class TypedNotificationPublisher<TNotification> : NotificationPublisher
        where TNotification : INotification
    {
        public override async ValueTask PublishAsync(INotification notification, IServiceProvider provider, CancellationToken cancellationToken)
        {
            foreach (var handler in provider.GetServices<INotificationHandler<TNotification>>())
            {
                await handler.HandleAsync((TNotification)notification, cancellationToken);
            }
        }
    }
}
