using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Components.Board;

/// <summary>
/// The board's message for a lost WebGL context (a phone backgrounding the tab, a GPU reset): shown while <see cref="Lost"/>
/// holds, offering to reload the page once the context has stayed away for <see cref="RestoreWait"/>.
/// </summary>
public sealed partial class BoardGraphicsLost : IDisposable
{
    private bool _shown;
    private bool _stalled;
    private CancellationTokenSource? _waiting;

    /// <summary>How long a lost WebGL context may take to come back before the board offers to reload the page.</summary>
    public static readonly TimeSpan RestoreWait = TimeSpan.FromSeconds(5);

    /// <summary>Whether the board's WebGL context is lost now.</summary>
    [Parameter]
    public bool Lost { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private TimeProvider Time { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Lost == _shown)
        {
            return;
        }

        _shown = Lost;
        StopWaiting();
        _stalled = false;
        if (Lost)
        {
            _waiting = new CancellationTokenSource();
            _ = OfferReloadAsync(_waiting.Token);
        }
    }

    /// <inheritdoc />
    public void Dispose() => StopWaiting();

    // The browser usually gives a lost context back within a second or two; when it doesn't, reloading the page is the way out.
    private async Task OfferReloadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(RestoreWait, Time, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await InvokeAsync(() =>
        {
            _stalled = true;
            StateHasChanged();
        });
    }

    private void StopWaiting()
    {
        _waiting?.Cancel();
        _waiting?.Dispose();
        _waiting = null;
    }

    private void Reload() => Navigation.Refresh(forceReload: true);
}
