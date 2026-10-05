using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Lenses;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.Core.Map;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.Client.Features.Games.Components.Board;

/// <summary>
/// The game board, drawn in 3D with WebGL. It turns the <see cref="PlayerView"/> into a <see cref="BoardScene"/>
/// (<see cref="BoardSceneBuilder"/>), sends it to the renderer only when the view changes, sends the <see cref="BoardOverlay"/>
/// only when the selection, its highlights or the lens change, sends the <see cref="SceneMotion"/> of <see cref="Motion"/> only when
/// it changes (after the scene of the same view), and reports clicked tiles through
/// <see cref="TileClicked"/>; panning, zooming and telling a drag from a click happen in the renderer. The tile under the mouse
/// (or last tapped) gets a <see cref="TileCard"/> in the corner and the pointer cursor when a click on it acts; a lost WebGL
/// context shows <see cref="BoardGraphicsLost"/> until the browser restores it. Its
/// <see cref="Board3DInteropService"/> comes from the component's own scope, so leaving the board unmounts the renderer.
/// </summary>
public sealed partial class GameBoard
{
    /// <summary>The zoom factor of one button press (the renderer zooms by the wheel and pinch itself).</summary>
    public const double ZoomStep = 1.2;

    private ElementReference _host;
    private ElementReference _anchor;
    private HexCoord? _anchored;
    private bool _mounted;
    private bool _unavailable;
    private PlayerView? _drawn;
    private BoardScene? _scene;
    private BoardFocus? _focus;
    private (PlayerView View, BoardSelection Selection, BoardHighlights Highlights, IMapLens? Lens)? _overlaid;
    private BoardMotion? _moved;
    private HexCoord? _hovered;
    private HexCoord? _tapped;
    private bool _pointer;
    private string? _announced;
    private bool _lost;

    /// <summary>The player's view to draw.</summary>
    [Parameter, EditorRequired]
    public PlayerView View { get; set; } = null!;

    /// <summary>The latest request to centre the board on a tile; a new request moves the camera.</summary>
    [Parameter]
    public BoardFocus? Focus { get; set; }

    /// <summary>The selected unit or city, ringed on the board.</summary>
    [Parameter]
    public BoardSelection Selection { get; set; } = BoardSelection.None;

    /// <summary>The tiles marked for the selection.</summary>
    [Parameter]
    public BoardHighlights Highlights { get; set; } = BoardHighlights.None;

    /// <summary>The active map lens, or <see langword="null"/> when none is on.</summary>
    [Parameter]
    public IMapLens? Lens { get; set; }

    /// <summary>
    /// What the board draws differently while animations play (<see cref="BoardAnimationService"/>); replaced, never changed, so a
    /// new one is sent to the renderer only when the reference changes.
    /// </summary>
    [Parameter]
    public BoardMotion Motion { get; set; } = BoardMotion.None;

    /// <summary>The ruleset's full health; hurt units get a health bar. 0 draws none.</summary>
    [Parameter]
    public int MaxHealth { get; set; }

    /// <summary>Whether tile clicks are ignored (e.g. while a command is being sent); panning and zooming still work.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Raised with the tile the player clicked (not dragged).</summary>
    [Parameter]
    public EventCallback<HexCoord> TileClicked { get; set; }

    /// <summary>A card kept beside <see cref="AnchorAt"/> as the camera moves (the attack forecast).</summary>
    [Parameter]
    public RenderFragment? Anchored { get; set; }

    /// <summary>The tile <see cref="Anchored"/> stays beside, or <see langword="null"/> for no card.</summary>
    [Parameter]
    public HexCoord? AnchorAt { get; set; }

    [Inject]
    private RulesetService Rulesets { get; set; } = null!;

    private Board3DInteropService Interop => ScopedServices.GetRequiredService<Board3DInteropService>();

    // The card of the hovered tile, else of the tile last tapped (touch never hovers), for the view and lens shown now.
    private TileCard? Card => (_hovered ?? _tapped) is { } tile && _drawn is not null
        ? TileCardBuilder.For(Rulesets.Ruleset, _drawn, tile, Lens)
        : null;

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
        await OverlayAsync();
        await MoveAsync();
        await FollowFocusAsync();
        await PointAsync();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            await AnchorAsync();
            return;
        }

        _mounted = await Interop.MountAsync(_host, new BoardEvents(OnTileClickedAsync, OnTileHoveredAsync, OnContextChangedAsync), CancellationToken.None);
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
        await OverlayAsync();
        await MoveAsync();
        await Interop.FitAsync(_scene!.Start, CancellationToken.None);
        await FollowFocusAsync();
        await AnchorAsync();
    }

    // The card is handed to the renderer once it is on the page, and let go when it goes.
    private async Task AnchorAsync()
    {
        var target = Anchored is null ? null : AnchorAt;
        if (!_mounted || target == _anchored)
        {
            return;
        }

        _anchored = target;
        await (target is { } tile
            ? Interop.AnchorAsync(_anchor, tile, CancellationToken.None)
            : Interop.UnanchorAsync(CancellationToken.None));
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
        _scene = BoardSceneBuilder.Build(View, MaxHealth);
        await Interop.RenderAsync(_scene, CancellationToken.None);
    }

    // The overlay is sent when the drawn view, the selection, its highlights or the lens change (all replaced, never changed).
    private async Task OverlayAsync()
    {
        var inputs = (View: _drawn!, Selection, Highlights, Lens);
        if (_overlaid is { } last && ReferenceEquals(last.View, inputs.View) && last.Selection == Selection
            && ReferenceEquals(last.Highlights, Highlights) && ReferenceEquals(last.Lens, Lens))
        {
            return;
        }

        _overlaid = inputs;
        await Interop.RenderOverlayAsync(BoardOverlayBuilder.Build(_drawn!, Selection, Highlights, Lens), CancellationToken.None);
    }

    // The motion is sent when it changes (the service replaces it at every step), after the scene it moves pieces of.
    private async Task MoveAsync()
    {
        if (ReferenceEquals(Motion, _moved))
        {
            return;
        }

        _moved = Motion;
        await Interop.RenderMotionAsync(SceneMotionBuilder.Build(Motion, _drawn), CancellationToken.None);
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

    // The pointer cursor is set when what a click on the hovered tile does changes: another tile, a new selection or highlights
    // (a move target), a new view (a piece arrived) or the board becoming busy.
    private async Task PointAsync()
    {
        var actionable = _hovered is { } tile && !Disabled && _drawn is not null && Acts(_drawn, tile);
        if (!_mounted || actionable == _pointer)
        {
            return;
        }

        _pointer = actionable;
        await Interop.SetPointerAsync(actionable, CancellationToken.None);
    }

    // A click on a tile acts when it selects a unit or city there, or is a move, attack, bridge or building target.
    private bool Acts(PlayerView view, HexCoord tile) =>
        view.UnitsAt(tile).Any() || view.CityAt(tile) is not null
        || Highlights.Reachable.Contains(tile) || Highlights.Attackable.Contains(tile)
        || Highlights.Bridgeable.Contains(tile) || Highlights.Placeable.Contains(tile);

    private async Task OnTileClickedAsync(HexCoord coord)
    {
        await InvokeAsync(() =>
        {
            _tapped = coord;
            _announced = _drawn is null ? null : TileCardBuilder.For(Rulesets.Ruleset, _drawn, coord, Lens)?.Text;
            StateHasChanged();
        });
        if (Disabled)
        {
            return;
        }

        await InvokeAsync(() => TileClicked.InvokeAsync(coord));
    }

    // A hover that ends (the pointer left, a drag started) also puts away a tapped tile's card.
    private Task OnTileHoveredAsync(HexCoord? coord) => InvokeAsync(async () =>
    {
        _hovered = coord;
        if (coord is null)
        {
            _tapped = null;
        }

        StateHasChanged();
        await PointAsync();
    });

    private Task OnContextChangedAsync(bool lost) => InvokeAsync(() =>
    {
        _lost = lost;
        StateHasChanged();
    });
}
