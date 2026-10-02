using Govern.Client.Features.Games.Models;
using Govern.Core.Map;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Govern.Client.Features.Games.Services;

/// <summary>
/// The game board's JavaScript (<c>wwwroot/js/board3d/board.js</c>, drawn with Three.js from <c>wwwroot/lib/three</c>): mounting a
/// WebGL canvas in an element, drawing a <see cref="BoardScene"/>, the <see cref="BoardOverlay"/> over it and the <see cref="SceneMotion"/> that animates it, moving the camera and reporting clicked and hovered tiles and a lost WebGL context (<see cref="BoardEvents"/>). One instance
/// per board: registered scoped and resolved from the board's own scope (<c>GameBoard</c> is an <c>OwningComponentBase</c>),
/// so it is disposed with the board. Every call degrades to a no-op when the module or WebGL can't run; <see cref="MountAsync"/>
/// says so.
/// </summary>
public sealed class Board3DInteropService(IJSRuntime js) : IAsyncDisposable
{
    /// <summary>The module path, relative to the app's base URL.</summary>
    public const string ModulePath = "./js/board3d/board.js";

    private IJSObjectReference? _module;
    private DotNetObjectReference<BoardRelay>? _relay;
    private ElementReference _host;
    private bool _disposed;

    /// <summary>Creates the board's canvas inside <paramref name="host"/> and starts listening for clicks.</summary>
    /// <param name="host">The element the canvas fills.</param>
    /// <param name="events">What the board reports back: clicks, hovering and a lost WebGL context.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns><see langword="true"/> when the board runs; <see langword="false"/> when the module or WebGL is unavailable.</returns>
    public async Task<bool> MountAsync(ElementReference host, BoardEvents events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        try
        {
            _host = host;
            _module = await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
            if (_disposed)
            {
                await ReleaseAsync();
                return false;
            }

            _relay = DotNetObjectReference.Create(new BoardRelay(events));
            var mounted = await _module.InvokeAsync<bool>("mount", cancellationToken, host, _relay);

            // The board may have gone away while the module was downloading; then nothing may stay mounted.
            if (_disposed)
            {
                await ReleaseAsync();
                return false;
            }

            return mounted;
        }
        catch (JSException)
        {
            await ReleaseAsync();
            return false;
        }
    }

    /// <summary>Draws a scene, replacing the one on the board.</summary>
    /// <param name="scene">The scene.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the scene is sent.</returns>
    public Task RenderAsync(BoardScene scene, CancellationToken cancellationToken) => CallAsync("render", cancellationToken, scene);

    /// <summary>Draws the overlay (lens, highlights, selection) over the scene, replacing the one before.</summary>
    /// <param name="overlay">The overlay.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the overlay is sent.</returns>
    public Task RenderOverlayAsync(BoardOverlay overlay, CancellationToken cancellationToken) =>
        CallAsync("renderOverlay", cancellationToken, overlay);

    /// <summary>Animates the scene: moves units, draws ghosts and effects, replacing the motion before.</summary>
    /// <param name="motion">The motion; an empty one puts every piece back on its tile.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the motion is sent.</returns>
    public Task RenderMotionAsync(SceneMotion motion, CancellationToken cancellationToken) =>
        CallAsync("renderMotion", cancellationToken, motion);

    /// <summary>
    /// Keeps a page element beside a tile as the camera moves (the attack forecast), through its <c>--anchor-x</c>,
    /// <c>--anchor-y</c> and <c>--anchor-flip</c> properties; it replaces the element kept before.
    /// </summary>
    /// <param name="element">The element, inside the board's frame.</param>
    /// <param name="tile">The tile.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the element is anchored.</returns>
    public Task AnchorAsync(ElementReference element, HexCoord tile, CancellationToken cancellationToken) =>
        CallAsync("anchor", cancellationToken, element, tile.Q, tile.R);

    /// <summary>Lets go of the element kept by <see cref="AnchorAsync"/>.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the element is let go.</returns>
    public Task UnanchorAsync(CancellationToken cancellationToken) => CallAsync("unanchor", cancellationToken);

    /// <summary>Moves the camera so a rectangle of the board fills the view.</summary>
    /// <param name="bounds">The rectangle in board units (x and depth).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the camera is moved.</returns>
    public Task FitAsync(BoardRect bounds, CancellationToken cancellationToken) => CallAsync("fit", cancellationToken, bounds);

    /// <summary>Centres the camera on a point of the board.</summary>
    /// <param name="x">The point's x in board units.</param>
    /// <param name="z">The point's depth in board units.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the camera is moved.</returns>
    public Task FocusAsync(double x, double z, CancellationToken cancellationToken) => CallAsync("focus", cancellationToken, x, z);

    /// <summary>Zooms around the middle of the view.</summary>
    /// <param name="factor">Above 1 zooms in, below 1 zooms out.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the camera is moved.</returns>
    public Task ZoomAsync(double factor, CancellationToken cancellationToken) => CallAsync("zoom", cancellationToken, factor);

    /// <summary>Shows the pointer cursor over the board while the hovered tile does something when clicked.</summary>
    /// <param name="actionable">Whether a click on the hovered tile acts (selects a piece, or moves, attacks or builds).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the cursor is set.</returns>
    public Task SetPointerAsync(bool actionable, CancellationToken cancellationToken) =>
        CallAsync("setPointer", cancellationToken, actionable);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await ReleaseAsync();
    }

    // Unmounts the board and lets go of the module and the click relay; safe to call more than once.
    private async Task ReleaseAsync()
    {
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("unmount", _host);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // The page is already gone.
        }
        catch (JSException)
        {
            // The board was never mounted.
        }

        _module = null;
        _relay?.Dispose();
        _relay = null;
    }

    private async Task CallAsync(string identifier, CancellationToken cancellationToken, params object?[] args)
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.InvokeVoidAsync(identifier, cancellationToken, [_host, .. args]);
        }
        catch (JSException)
        {
            // A failed draw leaves the board as it was; the next scene tries again.
        }
    }

    /// <summary>Receives what the board's JavaScript reports.</summary>
    /// <param name="events">Where each report goes.</param>
    private sealed class BoardRelay(BoardEvents events)
    {
        /// <summary>Called by the board's JavaScript when the player clicks a tile.</summary>
        /// <param name="q">The tile's axial column.</param>
        /// <param name="r">The tile's axial row.</param>
        /// <returns>A task that completes when the click is handled.</returns>
        [JSInvokable]
        public Task OnTileClicked(int q, int r) => events.TileClicked(new HexCoord(q, r));

        /// <summary>Called by the board's JavaScript when the mouse or pen comes over another tile.</summary>
        /// <param name="q">The tile's axial column.</param>
        /// <param name="r">The tile's axial row.</param>
        /// <returns>A task that completes when the hover is handled.</returns>
        [JSInvokable]
        public Task OnTileHovered(int q, int r) => events.TileHovered?.Invoke(new HexCoord(q, r)) ?? Task.CompletedTask;

        /// <summary>Called by the board's JavaScript when the pointer leaves the map or a drag starts.</summary>
        /// <returns>A task that completes when the hover is handled.</returns>
        [JSInvokable]
        public Task OnHoverEnded() => events.TileHovered?.Invoke(null) ?? Task.CompletedTask;

        /// <summary>Called by the board's JavaScript when the browser takes the WebGL context away.</summary>
        /// <returns>A task that completes when the loss is handled.</returns>
        [JSInvokable]
        public Task OnContextLost() => events.ContextChanged?.Invoke(true) ?? Task.CompletedTask;

        /// <summary>Called by the board's JavaScript once the WebGL context is back and the board is drawn again.</summary>
        /// <returns>A task that completes when the restore is handled.</returns>
        [JSInvokable]
        public Task OnContextRestored() => events.ContextChanged?.Invoke(false) ?? Task.CompletedTask;
    }
}
