namespace Govern.Api.Middlewares;

/// <summary>
/// Last-resort handler for exceptions outside the mediator (binding, auth, middleware): logs the exception and
/// returns a 500 ProblemDetails without any exception details. Malformed requests become 400. Requests aborted by
/// the client end with status 499 (for logs and metrics only; no body) and are logged at Debug, never as errors.
/// </summary>
internal sealed partial class ExceptionHandlingMiddleware(
    IProblemDetailsService problemDetailsService,
    ILogger<ExceptionHandlingMiddleware> logger) : IMiddleware
{
    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected: nobody will read a response. Stop quietly and record the de facto
            // "499 Client Closed Request" for request logs and metrics; this is not a server error.
            Log.RequestAborted(logger, context.Request.Method, context.Request.Path);
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            }
        }
        catch (BadHttpRequestException exception) when (!context.Response.HasStarted)
        {
            // Malformed input (e.g. invalid JSON body): a client error, not a server failure.
            Log.BadRequest(logger, context.Request.Method, context.Request.Path, exception.StatusCode);
            await WriteProblemAsync(context, exception.StatusCode, "The request is malformed.");
        }
        catch (Exception exception) when (!context.RequestAborted.IsCancellationRequested && !context.Response.HasStarted)
        {
            Log.UnhandledException(logger, context.Request.Method, context.Request.Path, exception);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    private async Task WriteProblemAsync(HttpContext context, int statusCode, string title)
    {
        context.Response.StatusCode = statusCode;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = { Status = statusCode, Title = title },
        });
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 3000, Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
        public static partial void UnhandledException(ILogger logger, string method, PathString path, Exception exception);

        [LoggerMessage(EventId = 3002, Level = LogLevel.Debug, Message = "Request {Method} {Path} was aborted by the client")]
        public static partial void RequestAborted(ILogger logger, string method, PathString path);

        [LoggerMessage(EventId = 3001, Level = LogLevel.Debug, Message = "Rejected malformed request {Method} {Path} with {StatusCode}")]
        public static partial void BadRequest(ILogger logger, string method, PathString path, int statusCode);
    }
}
