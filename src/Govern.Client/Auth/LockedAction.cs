using System.Runtime.ExceptionServices;
using Microsoft.JSInterop;

namespace Govern.Client.Auth;

/// <summary>
/// The .NET callback that <c>wwwroot/js/auth-lock.js</c> invokes once it holds the Web Lock. A failure of the action is
/// kept and rethrown by <see cref="GetResult"/>, so it reaches the caller with its own type instead of as a JavaScript error.
/// </summary>
/// <typeparam name="T">The result type.</typeparam>
public sealed class LockedAction<T>(Func<Task<T>> action)
{
    private T? _result;
    private ExceptionDispatchInfo? _failure;

    /// <summary>Whether the action has run.</summary>
    public bool HasRun { get; private set; }

    /// <summary>Runs the action; called from JavaScript while the lock is held.</summary>
    /// <returns>A task that completes when the action has finished; it never faults.</returns>
    [JSInvokable]
    public async Task RunLockedAsync()
    {
        HasRun = true;
        try
        {
            _result = await action();
        }
        catch (Exception exception)
        {
            // JS interop boundary: a rejected promise would turn the exception into a JSException.
            _failure = ExceptionDispatchInfo.Capture(exception);
        }
    }

    /// <summary>The action's result, or its exception rethrown.</summary>
    /// <returns>The result.</returns>
    /// <exception cref="InvalidOperationException">The action has not run.</exception>
    public T GetResult()
    {
        if (!HasRun)
        {
            throw new InvalidOperationException("The locked action has not run.");
        }

        _failure?.Throw();
        return _result!;
    }
}
