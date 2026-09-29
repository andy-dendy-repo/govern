using System.Collections.ObjectModel;
using System.Net;
using System.Text.Json;

namespace Govern.Client.Models;

/// <summary>
/// A failed Api call, read from the Api's RFC 9457 ProblemDetails (<c>title</c>, <c>code</c>, <c>errors</c>).
/// </summary>
/// <param name="Status">HTTP status code, or 0 when the Api could not be reached.</param>
/// <param name="Code">The stable error code (e.g. <c>user.email_taken</c>), or <see langword="null"/> when the response had none.</param>
/// <param name="Message">A message to show the user: the problem title, or a generic text for the status.</param>
/// <param name="FieldErrors">Validation messages per request field; empty unless the Api returned a validation problem.</param>
public sealed record ApiError(int Status, string? Code, string Message, IReadOnlyDictionary<string, string[]> FieldErrors)
{
    /// <summary>The Api could not be reached (network error, CORS failure or timeout).</summary>
    public static ApiError Network { get; } =
        new(0, null, "Could not reach the server. Check your connection and try again.", ReadOnlyDictionary<string, string[]>.Empty);

    /// <summary>Whether the Api could not be reached at all.</summary>
    public bool IsNetwork => Status == 0;

    /// <summary>An error with the generic message for <paramref name="status"/> and no code.</summary>
    /// <param name="status">The HTTP status.</param>
    /// <returns>The error.</returns>
    public static ApiError FromStatus(HttpStatusCode status) =>
        new((int)status, null, DefaultMessage((int)status), ReadOnlyDictionary<string, string[]>.Empty);

    /// <summary>
    /// Reads the ProblemDetails body of a failed response. An empty or non-JSON body (e.g. a rate-limit rejection) gives the
    /// generic message for the status.
    /// </summary>
    /// <param name="status">The HTTP status.</param>
    /// <param name="body">The response body.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>The error.</returns>
    public static async Task<ApiError> ReadAsync(HttpStatusCode status, Stream body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);

        ProblemBody? problem;
        try
        {
            problem = await JsonSerializer.DeserializeAsync<ProblemBody>(body, ApiJson.Options, cancellationToken);
        }
        catch (JsonException)
        {
            problem = null;
        }

        var fallback = FromStatus(status);
        return problem is null
            ? fallback
            : fallback with
            {
                Code = problem.Code,
                Message = string.IsNullOrWhiteSpace(problem.Title) ? fallback.Message : problem.Title,
                FieldErrors = problem.Errors ?? fallback.FieldErrors,
            };
    }

    private static string DefaultMessage(int status) => status switch
    {
        400 => "The request was not valid.",
        401 => "Please sign in again.",
        403 => "You are not allowed to do that.",
        404 => "Not found.",
        409 => "The request conflicts with the current state. Refresh and try again.",
        429 => "Too many requests. Wait a minute and try again.",
        >= 500 => "Something went wrong on the server. Try again later.",
        _ => "The request failed.",
    };

    private sealed record ProblemBody(string? Title, string? Code, Dictionary<string, string[]>? Errors);
}
