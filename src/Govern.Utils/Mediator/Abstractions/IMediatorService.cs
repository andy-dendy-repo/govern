using Govern.Utils.Results;

namespace Govern.Utils.Mediator.Abstractions;

/// <summary>
/// Dispatches requests to their handlers through the configured pipelines, and publishes notifications.
/// </summary>
public interface IMediatorService
{
    /// <summary>
    /// Sends <paramref name="request"/> through the pipelines to its single handler.
    /// </summary>
    /// <typeparam name="TRequest">The request type (inferred).</typeparam>
    /// <typeparam name="TResponse">The response type (inferred).</typeparam>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The handler's response, or the error from a pipeline or the handler.</returns>
    /// <exception cref="InvalidOperationException">No handler is registered for <typeparamref name="TRequest"/>.</exception>
    ValueTask<Result<TResponse>> SendAsync<TRequest, TResponse>(
        IRequest<TRequest, TResponse> request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TRequest, TResponse>
        where TResponse : IResponse;

    /// <summary>
    /// Publishes <paramref name="notification"/> to every handler registered for its runtime type, one after another.
    /// </summary>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>A task that completes when all handlers have run.</returns>
    ValueTask PublishAsync(INotification notification, CancellationToken cancellationToken);
}
