using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Lenses;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.Core.Map;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Govern.Client.Features.Games.Components.Board;

/// <summary>
/// The SVG hex board. It draws a <see cref="PlayerView"/> and reports clicked tiles through <see cref="TileClicked"/>; panning
/// and zooming are its own state. A press that moves more than <see cref="DragThreshold"/> pixels is a drag and never a click.
/// </summary>
public partial class GameBoard
{
    /// <summary>How far, in pixels, a press may move and still count as a click.</summary>
    public const double DragThreshold = 4;

    /// <summary>The zoom factor of one wheel notch or button press.</summary>
    public const double ZoomStep = 1.2;

    // Hexes of room left around the player's units and cities when the board first opens.
    private const int StartMargin = 3;

    // The size assumed until the element is measured (e.g. when JavaScript is unavailable).
    private static readonly BoardRect DefaultSize = new(0, 0, 800, 560);

    private ElementReference _board;
    private BoardViewport _viewport = new(1, 0, 0);
    private BoardRect _size = DefaultSize;
    private (double X, double Y)? _pressedAt;
    private long _pointerId;
    private (double X, double Y) _lastPointer;
    private bool _dragged;
    private bool _dirty = true;

    /// <summary>The player's view to draw.</summary>
    [Parameter, EditorRequired]
    public PlayerView View { get; set; } = null!;

    /// <summary>The selected unit or city, drawn highlighted.</summary>
    [Parameter, EditorRequired]
    public BoardSelection Selection { get; set; } = null!;

    /// <summary>The tiles to mark as reachable or attackable.</summary>
    [Parameter, EditorRequired]
    public BoardHighlights Highlights { get; set; } = null!;

    /// <summary>The map lens to draw over the terrain, or <see langword="null"/> for none.</summary>
    [Parameter]
    public IMapLens? Lens { get; set; }

    /// <summary>The ruleset's full health, so hurt units show a health bar; 0 draws none.</summary>
    [Parameter]
    public int MaxHealth { get; set; }

    /// <summary>Whether tile clicks are ignored (e.g. while a command is being sent); panning and zooming still work.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Raised with the tile the player clicked (not dragged).</summary>
    [Parameter]
    public EventCallback<HexCoord> TileClicked { get; set; }

    [Inject]
    private BoardInteropService Interop { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnParametersSet() => _dirty = true;

    // Pointer moves arrive often; the board renders only when something it shows changed.
    /// <inheritdoc />
    protected override bool ShouldRender()
    {
        var render = _dirty;
        _dirty = false;
        return render;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Interop.InitAsync(_board, CancellationToken.None);
            await FitAsync(StartBounds());
        }
    }

    // The board opens on the player's own units and cities (the rest is mostly fog); without any, on the whole map.
    private BoardRect StartBounds()
    {
        HexCoord[] own =
        [
            .. View.Units.Where(unit => unit.Owner == View.Viewer).Select(static unit => unit.Position),
            .. View.Cities.Where(city => city.Owner == View.Viewer).Select(static city => city.Position),
        ];
        return own.Length > 0 ? HexLayout.Around(own, StartMargin) : MapBounds;
    }

    private BoardRect MapBounds => HexLayout.Bounds(View.Width, View.Height);

    private Task FitMapAsync() => FitAsync(MapBounds);

    private async Task FitAsync(BoardRect bounds)
    {
        _size = await Interop.MeasureAsync(_board, CancellationToken.None) ?? _size;
        SetViewport(BoardViewport.Fit(bounds, _size.Width, _size.Height));
        StateHasChanged();
    }

    // Only the primary pointer pans, so a second finger can't make the board jump.
    private void OnPointerDown(PointerEventArgs args)
    {
        if (!args.IsPrimary)
        {
            return;
        }

        _pressedAt = _lastPointer = (args.ClientX, args.ClientY);
        _pointerId = args.PointerId;
        _dragged = false;
    }

    private void OnPointerMove(PointerEventArgs args)
    {
        if (args.PointerId != _pointerId)
        {
            return;
        }

        if (_pressedAt is not { } pressedAt || args.Buttons == 0)
        {
            _pressedAt = null;
            return;
        }

        _dragged |= Math.Abs(args.ClientX - pressedAt.X) + Math.Abs(args.ClientY - pressedAt.Y) > DragThreshold;
        if (_dragged)
        {
            SetViewport(_viewport.Pan(args.ClientX - _lastPointer.X, args.ClientY - _lastPointer.Y));
        }

        _lastPointer = (args.ClientX, args.ClientY);
    }

    // The click (if any) follows the release, so the drag flag stays until the next press.
    private void OnPointerUp(PointerEventArgs args)
    {
        if (args.PointerId == _pointerId)
        {
            _pressedAt = null;
        }
    }

    private async Task OnWheelAsync(WheelEventArgs args)
    {
        if (args.DeltaY == 0)
        {
            return;
        }

        _size = await Interop.MeasureAsync(_board, CancellationToken.None) ?? _size;
        SetViewport(_viewport.ZoomAt(args.ClientX - _size.Left, args.ClientY - _size.Top, WheelFactor(args)));
    }

    // One mouse notch (100 px, or 3 lines) zooms by ZoomStep; a trackpad's small deltas zoom smoothly instead of a step each.
    private static double WheelFactor(WheelEventArgs args)
    {
        var pixels = args.DeltaMode switch
        {
            1 => args.DeltaY * 100 / 3,
            2 => args.DeltaY * 800,
            _ => args.DeltaY,
        };
        return Math.Pow(ZoomStep, -Math.Clamp(pixels / 100, -3, 3));
    }

    private void ZoomAtCentre(double factor) => SetViewport(_viewport.ZoomAt(_size.Width / 2, _size.Height / 2, factor));

    private async Task OnTileClickedAsync(HexCoord coord)
    {
        if (_dragged || Disabled)
        {
            return;
        }

        await TileClicked.InvokeAsync(coord);
    }

    private void SetViewport(BoardViewport viewport)
    {
        _dirty |= viewport != _viewport;
        _viewport = viewport;
    }
}
