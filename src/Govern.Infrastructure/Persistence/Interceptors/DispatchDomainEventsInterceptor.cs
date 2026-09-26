using Govern.Domain.EntityBase;
using Govern.Utils.Mediator.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Govern.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Publishes the domain events of saved entities through the mediator after a successful
/// <c>SaveChangesAsync</c>, then clears them. Synchronous saves with pending events are rejected, because
/// dispatching asynchronous handlers synchronously would block (sync-over-async).
/// </summary>
internal sealed class DispatchDomainEventsInterceptor(IMediatorService mediator) : SaveChangesInterceptor
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
            await DispatchAsync(eventData.Context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchAsync(DbContext context, CancellationToken cancellationToken)
    {
        var entities = CollectEntities(context).ToList();
        var events = entities.SelectMany(static entity => entity.DomainEvents).ToList();
        entities.ForEach(static entity => entity.ClearDomainEvents());

        foreach (var domainEvent in events)
        {
            await mediator.PublishAsync(domainEvent, cancellationToken);
        }
    }

    private static IEnumerable<BaseEntity> CollectEntities(DbContext context) =>
        context.ChangeTracker.Entries<BaseEntity>()
            .Select(static entry => entry.Entity)
            .Where(static entity => entity.DomainEvents.Count > 0);
}
