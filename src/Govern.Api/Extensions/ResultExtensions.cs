using Govern.Utils.Results;

namespace Govern.Api.Extensions;

/// <summary>
/// Maps mediator results to HTTP results: success → the given result (200 OK by default),
/// failure → RFC 9457 ProblemDetails with the status for its <see cref="ErrorType"/> and a stable <c>code</c> extension.
/// </summary>
public static class ResultExtensions
{
    /// <param name="result">The mediator result.</param>
    /// <typeparam name="TValue">The response type.</typeparam>
    extension<TValue>(Result<TValue> result)
    {
        /// <summary>Converts the result to an HTTP result.</summary>
        /// <param name="onSuccess">Builds the success result; defaults to <c>200 OK</c> with the value.</param>
        /// <returns>The HTTP result.</returns>
        public IResult ToHttpResult(Func<TValue, IResult>? onSuccess = null) =>
            result.IsSuccess
                ? onSuccess?.Invoke(result.Value) ?? TypedResults.Ok(result.Value)
                : result.Error.ToProblem();
    }

    /// <param name="error">The error.</param>
    extension(Error error)
    {
        /// <summary>Converts the error to a ProblemDetails result (validation errors become a validation problem).</summary>
        /// <returns>The HTTP result.</returns>
        public IResult ToProblem()
        {
            var extensions = new Dictionary<string, object?> { ["code"] = error.Code };

            if (error.Type == ErrorType.Validation)
            {
                return TypedResults.ValidationProblem(
                    error.ValidationErrors.ToDictionary(),
                    title: error.Message,
                    extensions: extensions);
            }

            return TypedResults.Problem(
                title: error.Message,
                statusCode: StatusCode(error.Type),
                extensions: extensions);
        }
    }

    private static int StatusCode(ErrorType type) => type switch
    {
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status500InternalServerError,
    };
}
