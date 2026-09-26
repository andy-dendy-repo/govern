using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Results;
using Microsoft.Extensions.Logging;

namespace Govern.Utils.Mediator.Pipelines;

/// <summary>
/// Outermost pipeline: turns unhandled exceptions into <see cref="Error.Unexpected"/> so no exception details leak to
/// callers. The exception is logged once here. Cancellation requested by the caller is rethrown.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed partial class ErrorHandlingPipeline<TRequest, TResponse>(ILogger<ErrorHandlingPipeline<TRequest, TResponse>> logger)
    : IPipeline<TRequest, TResponse>
    where TRequest : IRequest<TRequest, TResponse>
    where TResponse : IResponse
{
    /// <inheritdoc />
    public async ValueTask<Result<TResponse>> HandleAsync(
        TRequest request,
        Func<ValueTask<Result<TResponse>>> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        try
        {
            return await next();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Log.UnhandledException(logger, typeof(TRequest).Name, exception);
            return Error.Unexpected();
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Unhandled exception while handling {RequestName}")]
        public static partial void UnhandledException(ILogger logger, string requestName, Exception exception);
    }
}
