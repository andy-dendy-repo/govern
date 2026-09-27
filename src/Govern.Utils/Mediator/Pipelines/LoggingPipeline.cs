using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Results;
using Microsoft.Extensions.Logging;

namespace Govern.Utils.Mediator.Pipelines;

/// <summary>
/// Logs one structured entry per request: its name, duration and outcome (success or error type and code).
/// Successes are Information; expected failures are Debug, except authentication/authorization failures (Warning,
/// useful to spot brute force). Request and response payloads are never logged.
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
            LogFailure(typeof(TRequest).Name, elapsed, result.Error);
        }

        return result;
    }

    private void LogFailure(string requestName, double elapsed, Error error)
    {
        switch (error.Type)
        {
            case ErrorType.Unauthorized or ErrorType.Forbidden or ErrorType.TooManyRequests:
                Log.AccessDenied(logger, requestName, elapsed, error.Type, error.Code);
                break;
            case ErrorType.Unexpected:
                // Already logged as Error (with the exception) by ErrorHandlingPipeline.
                Log.Unexpected(logger, requestName, elapsed, error.Code);
                break;
            default:
                Log.ExpectedFailure(logger, requestName, elapsed, error.Type, error.Code);
                break;
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "{RequestName} succeeded in {ElapsedMilliseconds:0.0} ms")]
        public static partial void Succeeded(ILogger logger, string requestName, double elapsedMilliseconds);

        [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "{RequestName} failed in {ElapsedMilliseconds:0.0} ms with {ErrorType} {ErrorCode}")]
        public static partial void ExpectedFailure(ILogger logger, string requestName, double elapsedMilliseconds, ErrorType errorType, string errorCode);

        [LoggerMessage(EventId = 12, Level = LogLevel.Warning, Message = "{RequestName} failed in {ElapsedMilliseconds:0.0} ms with {ErrorType} {ErrorCode}")]
        public static partial void AccessDenied(ILogger logger, string requestName, double elapsedMilliseconds, ErrorType errorType, string errorCode);

        [LoggerMessage(EventId = 13, Level = LogLevel.Information, Message = "{RequestName} failed in {ElapsedMilliseconds:0.0} ms with an unexpected error {ErrorCode}")]
        public static partial void Unexpected(ILogger logger, string requestName, double elapsedMilliseconds, string errorCode);
    }
}
