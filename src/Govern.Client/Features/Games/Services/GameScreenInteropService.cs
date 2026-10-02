using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Govern.Client.Features.Games.Services;

/// <summary>
/// The full-screen game view's JavaScript (<c>wwwroot/js/game-screen.js</c>): keeps the top strip's height in the screen's
/// <c>--gv-hud-top</c> property (the board's own controls sit below it), enters and leaves the browser's full screen, and reads
/// the reduced-motion preference. Every call degrades to a no-op when the module can't run; the screen still fills the window
/// without it.
/// </summary>
public sealed class GameScreenInteropService(IJSRuntime js) : IAsyncDisposable
{
    /// <summary>The module path, relative to the app's base URL.</summary>
    public const string ModulePath = "./js/game-screen.js";

    private IJSObjectReference? _module;
    private DotNetObjectReference<FullScreenRelay>? _relay;

    /// <summary>Starts following the strip's height and the screen's full-screen state.</summary>
    /// <param name="screen">The game screen element.</param>
    /// <param name="strip">The top strip element.</param>
    /// <param name="fullScreenChanged">Called with whether the screen is in full screen, whenever that changes.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Whether the browser can show the screen in full screen.</returns>
    public async Task<bool> WatchAsync(ElementReference screen, ElementReference strip, Action<bool> fullScreenChanged,
        CancellationToken cancellationToken)
    {
        try
        {
            _module ??= await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
            _relay?.Dispose();
            _relay = DotNetObjectReference.Create(new FullScreenRelay(fullScreenChanged));
            return await _module.InvokeAsync<bool>("watch", cancellationToken, screen, strip, _relay);
        }
        catch (JSException)
        {
            return false;
        }
    }

    /// <summary>Whether the player asked the browser for reduced motion (<c>prefers-reduced-motion: reduce</c>).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns><see langword="true"/> when reduced motion is asked for; <see langword="false"/> otherwise or when unknown.</returns>
    public async Task<bool> PrefersReducedMotionAsync(CancellationToken cancellationToken)
    {
        try
        {
            _module ??= await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
            return await _module.InvokeAsync<bool>("prefersReducedMotion", cancellationToken);
        }
        catch (JSException)
        {
            return false;
        }
    }

    /// <summary>Stops following the screen and lets go of the full-screen callback (the game table is gone).</summary>
    /// <param name="screen">The game screen element.</param>
    /// <returns>A task that completes when the screen is no longer followed.</returns>
    public async Task UnwatchAsync(ElementReference screen)
    {
        try
        {
            await CallAsync("unwatch", CancellationToken.None, screen);
        }
        catch (JSDisconnectedException)
        {
            // The page is already gone.
        }

        _relay?.Dispose();
        _relay = null;
    }

    /// <summary>Shows the screen in the browser's full screen.</summary>
    /// <param name="screen">The game screen element.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the browser answered.</returns>
    public Task EnterAsync(ElementReference screen, CancellationToken cancellationToken) => CallAsync("enter", cancellationToken, screen);

    /// <summary>Leaves the browser's full screen.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the browser answered.</returns>
    public Task ExitAsync(CancellationToken cancellationToken) => CallAsync("exit", cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _relay?.Dispose();
        _relay = null;
        try
        {
            if (_module is not null)
            {
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // The page is already gone.
        }

        _module = null;
    }

    private async Task CallAsync(string identifier, CancellationToken cancellationToken, params object?[] args)
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.InvokeVoidAsync(identifier, cancellationToken, args);
        }
        catch (JSException)
        {
            // The browser refused; the screen stays as it is.
        }
    }

    /// <summary>Receives full-screen changes from the screen's JavaScript.</summary>
    /// <param name="changed">Called with whether the screen is in full screen.</param>
    private sealed class FullScreenRelay(Action<bool> changed)
    {
        /// <summary>Called by the screen's JavaScript when the screen enters or leaves full screen.</summary>
        /// <param name="fullScreen">Whether the screen is in full screen now.</param>
        [JSInvokable]
        public void OnFullScreenChanged(bool fullScreen) => changed(fullScreen);
    }
}
