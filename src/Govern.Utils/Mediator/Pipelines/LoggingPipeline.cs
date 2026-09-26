using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Results;
using Microsoft.Extensions.Logging;

namespace Govern.Utils.Mediator.Pipelines;

/// <summary>
/// Logs one structured entry per request: its name, duration and outcome (success or error type and code).
/// Request and response payloads are never logged.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed partial class LoggingPipeline<TRequest, TResponse>(
    ILogger<LoggingPipeline<TRequest, TResponse>> logger,
    TimeProvider timeProvider)
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

        var started = timeProvider.GetTimestamp();
        var result = await next();
        var elapsed = timeProvider.GetElapsedTime(started).TotalMilliseconds;

        if (result.IsSuccess)
        {
            Log.Succeeded(logger, typeof(TRequest).Name, elapsed);
        }
        else
        {
            Log.Failed(logger, typeof(TRequest).Name, elapsed, result.Error.Type, result.Error.Code);
        }

        return result;
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "{RequestName} succeeded in {ElapsedMilliseconds:0.0} ms")]
        public static partial void Succeeded(ILogger logger, string requestName, double elapsedMilliseconds);

        [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "{RequestName} failed in {ElapsedMilliseconds:0.0} ms with {ErrorType} {ErrorCode}")]
        public static partial void Failed(ILogger logger, string requestName, double elapsedMilliseconds, ErrorType errorType, string errorCode);
    }
}
