using Microsoft.JSInterop;

namespace Govern.Client.Auth;

/// <summary>
/// <see cref="ICrossTabLock"/> on the browser's Web Locks API (<c>navigator.locks</c>) through <c>wwwroot/js/auth-lock.js</c>.
/// </summary>
/// <remarks>
/// A browser without Web Locks runs the action directly (the module's fallback): only the cross-tab race remains. When the
/// module fails to load or the lock is refused, <see cref="CrossTabLockUnavailableException"/> is thrown instead, so a
/// refresh is not attempted without the lock.
/// </remarks>
public sealed class WebLocksCrossTabLock(IJSRuntime js) : ICrossTabLock
{
    /// <summary>The name of the Web Lock.</summary>
    public const string LockName = "govern.auth.refresh";

    /// <summary>The module path, relative to the app's base URL.</summary>
    public const string ModulePath = "./js/auth-lock.js";

    private Task<IJSObjectReference>? _module;

    /// <inheritdoc />
    /// <exception cref="CrossTabLockUnavailableException">The lock could not be taken, so the action did not run.</exception>
    public async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var locked = new LockedAction<T>(action);
        using var callback = DotNetObjectReference.Create(locked);
        try
        {
            _module ??= js.InvokeAsync<IJSObjectReference>("import", CancellationToken.None, ModulePath).AsTask();
            var module = await _module;
            await module.InvokeVoidAsync("runExclusive", cancellationToken, LockName, callback);
        }
        catch (JSException exception) when (!locked.HasRun)
        {
            _module = null;
            throw new CrossTabLockUnavailableException("The Web Lock could not be taken.", exception);
        }
        catch (JSException) when (locked.HasRun)
        {
            // The action ran and its outcome is kept in the callback; only releasing the lock failed.
        }

        return locked.HasRun
            ? locked.GetResult()
            : throw new CrossTabLockUnavailableException("The Web Lock module returned without running the action.");
    }
}
