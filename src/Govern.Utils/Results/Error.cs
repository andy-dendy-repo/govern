namespace Govern.Utils.Results;

/// <summary>
/// An expected failure returned instead of throwing. <see cref="Code"/> is stable and machine-readable;
/// <see cref="Message"/> is safe to show to API clients.
/// </summary>
/// <param name="Code">Stable, snake_case error code (e.g. <c>user.email_taken</c>).</param>
/// <param name="Message">Human-readable description without sensitive data.</param>
/// <param name="Type">Category used to choose the HTTP status code.</param>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    private static readonly IReadOnlyDictionary<string, string[]> NoValidationErrors = new Dictionary<string, string[]>();

    /// <summary>
    /// Field errors keyed by property name; empty unless <see cref="Type"/> is <see cref="ErrorType.Validation"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; init; } = NoValidationErrors;

    /// <summary>Creates a validation error with the given field errors.</summary>
    /// <param name="errors">Field errors keyed by property name.</param>
    /// <returns>An error of type <see cref="ErrorType.Validation"/>.</returns>
    public static Error Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new("validation_failed", "One or more validation errors occurred.", ErrorType.Validation) { ValidationErrors = errors };

    /// <summary>Creates a not-found error.</summary>
    /// <param name="code">Stable error code.</param>
    /// <param name="message">Client-safe message.</param>
    /// <returns>An error of type <see cref="ErrorType.NotFound"/>.</returns>
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    /// <summary>Creates a conflict error.</summary>
    /// <param name="code">Stable error code.</param>
    /// <param name="message">Client-safe message.</param>
    /// <returns>An error of type <see cref="ErrorType.Conflict"/>.</returns>
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    /// <summary>Creates an unauthorized error.</summary>
    /// <param name="code">Stable error code.</param>
    /// <param name="message">Client-safe message.</param>
    /// <returns>An error of type <see cref="ErrorType.Unauthorized"/>.</returns>
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    /// <summary>Creates a forbidden error.</summary>
    /// <param name="code">Stable error code.</param>
    /// <param name="message">Client-safe message.</param>
    /// <returns>An error of type <see cref="ErrorType.Forbidden"/>.</returns>
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    /// <summary>Creates the generic unexpected error; never carries exception details.</summary>
    /// <returns>An error of type <see cref="ErrorType.Unexpected"/>.</returns>
    public static Error Unexpected() => new("unexpected", "An unexpected error occurred.", ErrorType.Unexpected);
}
