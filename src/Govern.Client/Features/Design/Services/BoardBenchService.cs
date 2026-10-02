using Govern.Client.Features.Design.Models;
using Govern.Client.Features.Games.Services;
using Microsoft.JSInterop;

namespace Govern.Client.Features.Design.Services;

/// <summary>
/// Measures the 3D board on the bench page (<c>/design/board</c>) through the board's own module
/// (<see cref="Board3DInteropService.ModulePath"/>, its <c>measure</c> export). Only the page uses it, and the page renders only in
/// Development.
/// </summary>
public sealed class BoardBenchService(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    /// <summary>Measures the 3D board on the page: the last scene applied, a full rebuild and a series of panning frames.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    /// The numbers, with <see cref="BoardBenchResult.SceneBuild"/> left at 0 for the caller; <see langword="null"/> when no board is
    /// mounted or measuring failed (the WebGL context was lost, for example).
    /// </returns>
    public async Task<BoardBenchResult?> MeasureAsync(CancellationToken cancellationToken)
    {
        try
        {
            _module ??= await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, Board3DInteropService.ModulePath);
            return await _module.InvokeAsync<BoardBenchResult?>("measure", cancellationToken);
        }
        catch (JSException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The page is already gone.
        }

        _module = null;
    }
}
