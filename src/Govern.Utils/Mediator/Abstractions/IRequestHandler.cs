namespace Govern.Utils.Mediator.Abstractions;

public interface IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest
    where TResponse : IResponse
{
    ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}