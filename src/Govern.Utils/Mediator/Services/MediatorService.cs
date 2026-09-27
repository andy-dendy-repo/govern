using System.Collections.Concurrent;
using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Mediator.Options;
using Govern.Utils.Mediator.Publishing;
using Govern.Utils.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Govern.Utils.Mediator.Services;

/// <inheritdoc />
/// <remarks>
/// Registered as scoped. With <see cref="MediatorOptions.UseParentScope"/> it resolves handlers from the caller's scope,
/// otherwise from a new, separate DI scope per call.
/// </remarks>
public sealed class MediatorService(IServiceProvider serviceProvider, IOptions<MediatorOptions> options) : IMediatorService
{
    private static readonly ConcurrentDictionary<Type, NotificationPublisher> Publishers = new();

    /// <inheritdoc />
    public async ValueTask<Result<TResponse>> SendAsync<TRequest, TResponse>(
        IRequest<TRequest, TResponse> request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TRequest, TResponse>
        where TResponse : IResponse
    {
        ArgumentNullException.ThrowIfNull(request);
        var typedRequest = (TRequest)request;

        if (options.Value.UseParentScope)
        {
            return await DispatchAsync<TRequest, TResponse>(serviceProvider, typedRequest, cancellationToken);
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        return await DispatchAsync<TRequest, TResponse>(scope.ServiceProvider, typedRequest, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask PublishAsync(INotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var publisher = Publishers.GetOrAdd(notification.GetType(), NotificationPublisher.Create);

        if (options.Value.UseParentScope)
        {
            await publisher.PublishAsync(notification, serviceProvider, cancellationToken);
            return;
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        await publisher.PublishAsync(notification, scope.ServiceProvider, cancellationToken);
    }

    private static ValueTask<Result<TResponse>> DispatchAsync<TRequest, TResponse>(
        IServiceProvider provider,
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TRequest, TResponse>
        where TResponse : IResponse
    {
        var handler = provider.GetService<IRequestHandler<TRequest, TResponse>>()
            ?? throw new InvalidOperationException($"No handler is registered for {typeof(TRequest).FullName}.");

        Func<ValueTask<Result<TResponse>>> next = () => handler.HandleAsync(request, cancellationToken);

        foreach (var pipeline in provider.GetServices<IPipeline<TRequest, TResponse>>().Reverse())
        {
            var inner = next;
            next = () => pipeline.HandleAsync(request, inner, cancellationToken);
        }

        return next();
    }
}
