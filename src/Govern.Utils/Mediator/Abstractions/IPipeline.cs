using Govern.Utils.Results;

namespace Govern.Utils.Mediator.Abstractions;

/// <summary>
/// A step wrapped around every request handler (logging, error handling, validation, …).
/// Pipelines run in the order they are registered in <see cref="Options.MediatorOptions"/>; the first is the outermost.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IPipeline<TRequest, TResponse>
    where TRequest : IRequest<TRequest, TResponse>
    where TResponse : IResponse
{
    /// <summary>
    /// Runs this step. Call <paramref name="next"/> to continue, or return a failure to short-circuit.
    /// </summary>
    /// <param name="request">The request being dispatched.</param>
    /// <param name="next">Invokes the next pipeline or, for the innermost one, the handler.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The result of the chain.</returns>
    ValueTask<Result<TResponse>> HandleAsync(
        TRequest request,
        Func<ValueTask<Result<TResponse>>> next,
        CancellationToken cancellationToken);
}
