using Govern.Utils.Mediator.Abstractions;

namespace Govern.Utils.Mediator.Services;

public class MediatorService : IMediatorService
{
    public ValueTask SendAsync<TRequestHandler, TRequest, TResponse>(TRequest request, CancellationToken cancellationToken) where TRequestHandler : IRequestHandler<TRequest, TResponse> where TRequest : IRequest where TResponse : IResponse
    {
        throw new NotImplementedException();
    }
}