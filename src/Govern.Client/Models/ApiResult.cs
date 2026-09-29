using System.Diagnostics.CodeAnalysis;

namespace Govern.Client.Models;

/// <summary>
/// The outcome of an Api call without a response body: success, or an <see cref="ApiError"/>. Typed clients return results
/// instead of throwing <c>HttpRequestException</c>.
/// </summary>
public sealed class ApiResult
{
    private static readonly ApiResult Success = new(null);

    private ApiResult(ApiError? error)
    {
        Error = error;
        IsSuccess = error is null;
    }

    /// <summary>The error; <see langword="null"/> on success.</summary>
    public ApiError? Error { get; }

    /// <summary>Whether the call succeeded.</summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    /// <summary>A successful result.</summary>
    /// <returns>The result.</returns>
    public static ApiResult Ok() => Success;

    /// <summary>A successful result carrying <paramref name="value"/>.</summary>
    /// <param name="value">The response body.</param>
    /// <typeparam name="T">The response type.</typeparam>
    /// <returns>The result.</returns>
    public static ApiResult<T> Ok<T>(T value) => new(value, null);

    /// <summary>A failed result.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The result.</returns>
    public static ApiResult Fail(ApiError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ApiResult(error);
    }

    /// <summary>A failed result for a call that returns <typeparamref name="T"/>.</summary>
    /// <param name="error">The error.</param>
    /// <typeparam name="T">The response type.</typeparam>
    /// <returns>The result.</returns>
    public static ApiResult<T> Fail<T>(ApiError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ApiResult<T>(default, error);
    }
}
