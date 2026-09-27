using System.Diagnostics.CodeAnalysis;

namespace Govern.Utils.Results;

/// <summary>
/// The outcome of an operation: either a <see cref="Value"/> or an <see cref="Error"/>.
/// Use it for expected failures (validation, not found, conflict); throw only for programming errors.
/// </summary>
/// <typeparam name="TValue">Type of the success value.</typeparam>
public sealed class Result<TValue>
{
    private readonly TValue? _value;

    private Result(TValue value) => _value = value;

    private Result(Error error) => Error = error;

    /// <summary>
    /// <see langword="true"/> when the operation succeeded and <see cref="Value"/> is available.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>
    /// The failure, or <see langword="null"/> on success.
    /// </summary>
    public Error? Error { get; }

    /// <summary>
    /// The success value.
    /// </summary>
    /// <exception cref="InvalidOperationException">The result is a failure.</exception>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read the value of a failed result ({Error.Code}).");

    /// <summary>Wraps a success value.</summary>
    /// <param name="value">The value.</param>
    public static implicit operator Result<TValue>(TValue value) => new(value);

    /// <summary>Wraps a failure.</summary>
    /// <param name="error">The error.</param>
    public static implicit operator Result<TValue>(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<TValue>(error);
    }

    /// <summary>Named alternative to the implicit success conversion.</summary>
    /// <param name="value">The value.</param>
    /// <returns>A successful result.</returns>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Factory mirrors the implicit operator (CA2225).")]
    public static Result<TValue> FromValue(TValue value) => value;

    /// <summary>Named alternative to the implicit failure conversion.</summary>
    /// <param name="error">The error.</param>
    /// <returns>A failed result.</returns>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Factory mirrors the implicit operator (CA2225).")]
    public static Result<TValue> FromError(Error error) => error;

    /// <summary>Projects the result into a single value.</summary>
    /// <typeparam name="TResult">Type of the projection.</typeparam>
    /// <param name="onSuccess">Called with the value on success.</param>
    /// <param name="onFailure">Called with the error on failure.</param>
    /// <returns>The projected value.</returns>
    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<Error, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(_value!) : onFailure(Error);
    }
}
