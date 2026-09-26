namespace Govern.Utils.Mediator.Abstractions;

/// <summary>
/// Handles a single request type and produces its response.
/// </summary>
/// <typeparam name="TRequest">The handled request type.</typeparam>
/// <typeparam name="TResponse">The produced response type.</typeparam>
public interface IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
    where TResponse : IResponse
{
    /// <summary>
    /// Handles <paramref name="request"/>.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The response for the request.</returns>
    ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
