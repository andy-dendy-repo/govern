using Govern.Client.Features.Games.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Govern.Client.Features.Games.Services;

/// <summary>
/// The game board's JavaScript (<c>wwwroot/js/board.js</c>): a non-passive wheel listener, so zooming never scrolls the
/// page, and measuring the board element. Every call degrades to a no-op when the module can't run, so the board still works
/// (with a default size) without it.
/// </summary>
public sealed class BoardInteropService(IJSRuntime js) : IAsyncDisposable
{
    /// <summary>The module path, relative to the app's base URL.</summary>
    public const string ModulePath = "./js/board.js";

    private Task<IJSObjectReference>? _module;

    /// <summary>Stops the page from scrolling while the wheel is used over <paramref name="board"/>.</summary>
    /// <param name="board">The board element.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the listener is added, or was not.</returns>
    public async Task InitAsync(ElementReference board, CancellationToken cancellationToken)
    {
        try
        {
            var module = await ModuleAsync();
            await module.InvokeVoidAsync("init", cancellationToken, board);
        }
        catch (JSException)
        {
            ForgetFailedImport();
        }
    }

    /// <summary>Reads the position and size of <paramref name="board"/> in the window.</summary>
    /// <param name="board">The board element.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The element's rectangle, or <see langword="null"/> when it could not be measured.</returns>
    public async Task<BoardRect?> MeasureAsync(ElementReference board, CancellationToken cancellationToken)
    {
        try
        {
            var module = await ModuleAsync();
            return await module.InvokeAsync<BoardRect?>("measure", cancellationToken, board);
        }
        catch (JSException)
        {
            ForgetFailedImport();
            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is not { IsCompletedSuccessfully: true } module)
        {
            return;
        }

        try
        {
            await (await module).DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The page is already gone.
        }
    }

    // Only a failed import is retried next time; a call that failed on a loaded module keeps the module.
    private void ForgetFailedImport()
    {
        if (_module is { IsFaulted: true })
        {
            _module = null;
        }
    }

    private Task<IJSObjectReference> ModuleAsync() =>
        _module ??= js.InvokeAsync<IJSObjectReference>("import", CancellationToken.None, ModulePath).AsTask();
}
