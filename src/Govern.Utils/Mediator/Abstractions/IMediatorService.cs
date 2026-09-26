namespace Govern.Utils.Mediator.Abstractions;

/// <summary>
/// Dispatches requests to their handlers.
/// </summary>
public interface IMediatorService
{
    /// <summary>
    /// Sends <paramref name="request"/> to the handler of type <typeparamref name="TRequestHandler"/>.
    /// </summary>
    /// <typeparam name="TRequestHandler">The handler that processes the request.</typeparam>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type produced by the handler.</typeparam>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    ValueTask SendAsync<TRequestHandler, TRequest, TResponse>(TRequest request, CancellationToken cancellationToken)
        where TRequest : IRequest
        where TResponse : IResponse
        where TRequestHandler : IRequestHandler<TRequest, TResponse>;
}
