using System.Diagnostics.CodeAnalysis;

namespace Govern.Client.Models;

/// <summary>
/// The outcome of an Api call that returns <typeparamref name="T"/>: the value, or an <see cref="ApiError"/>. Create it with
/// <see cref="ApiResult.Ok{T}(T)"/> or <see cref="ApiResult.Fail{T}(ApiError)"/>.
/// </summary>
/// <typeparam name="T">The response type.</typeparam>
public sealed class ApiResult<T>
{
    internal ApiResult(T? value, ApiError? error)
    {
        Value = value;
        Error = error;
        IsSuccess = error is null;
    }

    /// <summary>The response body; <see langword="default"/> on failure.</summary>
    public T? Value { get; }

    /// <summary>The error; <see langword="null"/> on success.</summary>
    public ApiError? Error { get; }

    /// <summary>Whether the call succeeded.</summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }
}
