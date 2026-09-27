using Govern.Domain.EntityBase;
using Govern.Utils.Mediator.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Govern.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Publishes the domain events of saved entities through the mediator after a successful
/// <c>SaveChangesAsync</c>, then clears them. Synchronous saves with pending events are rejected, because
/// dispatching asynchronous handlers synchronously would block (sync-over-async).
/// </summary>
/// <remarks>
/// The data is already committed when events are dispatched, so a failing handler must not fail the request:
/// each event is published independently, failures are logged, and the request's cancellation is not applied.
/// Delivery is best-effort; use an outbox (<c>Infrastructure/Background</c>) when an event must never be lost.
/// </remarks>
internal sealed partial class DispatchDomainEventsInterceptor(
    IMediatorService mediator,
    ILogger<DispatchDomainEventsInterceptor> logger) : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null && CollectEntities(eventData.Context).Any())
        {
            throw new InvalidOperationException("Entities with domain events must be saved with SaveChangesAsync.");
        }

        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await DispatchAsync(eventData.Context);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchAsync(DbContext context)
    {
        var entities = CollectEntities(context).ToList();
        var events = entities.SelectMany(static entity => entity.DomainEvents).ToList();
        entities.ForEach(static entity => entity.ClearDomainEvents());

        foreach (var domainEvent in events)
        {
            try
            {
                await mediator.PublishAsync(domainEvent, CancellationToken.None);
            }
            catch (Exception exception)
            {
                Log.DispatchFailed(logger, domainEvent.GetType().Name, exception);
            }
        }
    }

    private static IEnumerable<BaseEntity> CollectEntities(DbContext context) =>
        context.ChangeTracker.Entries<BaseEntity>()
            .Select(static entry => entry.Entity)
            .Where(static entity => entity.DomainEvents.Count > 0);

    private static partial class Log
    {
        [LoggerMessage(EventId = 2001, Level = LogLevel.Error, Message = "Handling domain event {EventName} failed after the data was saved")]
        public static partial void DispatchFailed(ILogger logger, string eventName, Exception exception);
    }
}
