namespace Govern.Utils.Mediator.Abstractions;

public interface IMediatorService
{
    ValueTask SendAsync<TRequestHandler, TRequest, TResponse>(TRequest request, CancellationToken cancellationToken)
        where TRequest : IRequest
        where TResponse : IResponse
        where TRequestHandler : IRequestHandler<TRequest, TResponse>;
}