namespace Govern.Utils.Mediator.Abstractions;

/// <summary>
/// A request dispatched through <see cref="IMediatorService"/>. It is self-referencing, so the handler type is known
/// at compile time: <c>public sealed record SignUpRequest(...) : IRequest&lt;SignUpRequest, SignUpResponse&gt;;</c>
/// </summary>
/// <typeparam name="TRequest">The implementing request type itself.</typeparam>
/// <typeparam name="TResponse">The response produced by the request's handler.</typeparam>
public interface IRequest<TRequest, TResponse>
    where TRequest : IRequest<TRequest, TResponse>
    where TResponse : IResponse
{
}
