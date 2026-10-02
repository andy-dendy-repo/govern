using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.Core.Map;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.Client.Features.Games.Components.Board;

/// <summary>
/// The 3D hex board (F15 preview). It turns the <see cref="PlayerView"/> into a <see cref="BoardScene"/>
/// (<see cref="BoardSceneBuilder"/>), sends it to the renderer only when the view changes, and reports clicked tiles through
/// <see cref="TileClicked"/>; panning, zooming and telling a drag from a click happen in the renderer. Its
/// <see cref="Board3DInteropService"/> comes from the component's own scope, so leaving the board unmounts the renderer.
/// </summary>
public sealed partial class GameBoard3D
{
    private ElementReference _host;
    private bool _mounted;
    private bool _unavailable;
    private PlayerView? _drawn;
    private BoardScene? _scene;
    private BoardFocus? _focus;

    /// <summary>The player's view to draw.</summary>
    [Parameter, EditorRequired]
    public PlayerView View { get; set; } = null!;

    /// <summary>The latest request to centre the board on a tile; a new request moves the camera.</summary>
    [Parameter]
    public BoardFocus? Focus { get; set; }

    /// <summary>Whether tile clicks are ignored (e.g. while a command is being sent); panning and zooming still work.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Raised with the tile the player clicked (not dragged).</summary>
    [Parameter]
    public EventCallback<HexCoord> TileClicked { get; set; }

    private Board3DInteropService Interop => ScopedServices.GetRequiredService<Board3DInteropService>();

    // A focus request older than the board (an earlier game log click) doesn't move it; the board opens on the player's pieces.
    /// <inheritdoc />
    protected override void OnInitialized() => _focus = Focus;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (!_mounted)
        {
            return;
        }

        await DrawAsync();
        await FollowFocusAsync();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _mounted = await Interop.MountAsync(_host, OnTileClickedAsync, CancellationToken.None);
        if (IsDisposed)
        {
            return;
        }

        if (!_mounted)
        {
            _unavailable = true;
            StateHasChanged();
            return;
        }

        await DrawAsync();
        await Interop.FitAsync(_scene!.Start, CancellationToken.None);
        await FollowFocusAsync();
    }

    // The scene is built and sent only for a new view (a new version: the live update echoing the player's own command carries
    // the same one); selection and other UI changes don't touch it (yet).
    private async Task DrawAsync()
    {
        if (ReferenceEquals(View, _drawn) || View.Version == _drawn?.Version)
        {
            return;
        }

        _drawn = View;
        _scene = BoardSceneBuilder.Build(View);
        await Interop.RenderAsync(_scene, CancellationToken.None);
    }

    private async Task FollowFocusAsync()
    {
        if (Focus is { } focus && focus != _focus)
        {
            _focus = focus;
            var (x, z) = BoardSceneBuilder.Center(focus.Tile);
            await Interop.FocusAsync(x, z, CancellationToken.None);
        }
    }

    private Task FitMapAsync() => _scene is null ? Task.CompletedTask : Interop.FitAsync(_scene.Map, CancellationToken.None);

    private Task ZoomAsync(double factor) => Interop.ZoomAsync(factor, CancellationToken.None);

    private async Task OnTileClickedAsync(HexCoord coord)
    {
        if (Disabled)
        {
            return;
        }

        await InvokeAsync(() => TileClicked.InvokeAsync(coord));
    }
}
