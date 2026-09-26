namespace Govern.Utils.Results;

/// <summary>
/// Category of an <see cref="Error"/>; the Api maps it to an HTTP status code.
/// </summary>
public enum ErrorType
{
    /// <summary>The request is invalid (400).</summary>
    Validation,

    /// <summary>The requested resource does not exist (404).</summary>
    NotFound,

    /// <summary>The request conflicts with the current state, e.g. a duplicate (409).</summary>
    Conflict,

    /// <summary>The caller is not authenticated or the credentials are invalid (401).</summary>
    Unauthorized,

    /// <summary>The caller is authenticated but not allowed to perform the operation (403).</summary>
    Forbidden,

    /// <summary>An unexpected failure; details are logged, never returned (500).</summary>
    Unexpected,
}
