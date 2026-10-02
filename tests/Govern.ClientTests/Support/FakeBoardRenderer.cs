using System.Reflection;
using System.Runtime.ExceptionServices;
using Bunit.JSInterop;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.Core.Map;

namespace Govern.ClientTests.Support;

/// <summary>
/// The game board's renderer (<c>wwwroot/js/board3d</c>) as a test double: the module mounts (WebGL works), every call is
/// recorded, and a tile is clicked the way the renderer does it, through the click relay the board mounted with. Clicks run
/// outside bUnit's event dispatch, so a click that fails later (after an await) is rethrown by the next read of what the board
/// sent, or by <see cref="ThrowIfAnyClickFailed"/>.
/// </summary>
public sealed class FakeBoardRenderer
{
    private readonly List<Task> _clicks = [];

    private static readonly string[] VoidCalls =
        ["render", "renderOverlay", "renderMotion", "fit", "focus", "zoom", "anchor", "unanchor", "unmount"];

    /// <summary>Sets up the renderer's module on <paramref name="js"/>.</summary>
    /// <param name="js">The test context's JS interop.</param>
    public FakeBoardRenderer(BunitJSInterop js)
    {
        Module = js.SetupModule(Board3DInteropService.ModulePath);
        Module.Setup<bool>("mount", static _ => true).SetResult(true);
        foreach (var call in VoidCalls)
        {
            Module.SetupVoid(call, static _ => true).SetVoidResult();
        }
    }

    /// <summary>The module, for the calls a test reads directly.</summary>
    public BunitJSModuleInterop Module { get; }

    /// <summary>The scene drawn last.</summary>
    public BoardScene Scene => Last<BoardScene>("render");

    /// <summary>The overlay drawn last (the selection, its highlights and the lens).</summary>
    public BoardOverlay Overlay => Last<BoardOverlay>("renderOverlay");

    /// <summary>The motion sent last.</summary>
    public SceneMotion Motion => Last<SceneMotion>("renderMotion");

    /// <summary>The tiles of the last overlay tinted with <paramref name="tint"/> (such as <c>--gv-overlay-reachable</c>).</summary>
    /// <param name="tint">The tint token.</param>
    /// <returns>The tinted tiles.</returns>
    public IReadOnlyList<OverlayTile> Tinted(string tint) => [.. Overlay.Tiles.Where(tile => tile.Tint == tint)];

    /// <summary>The selection's highlights in the last overlay (reachable, attackable, target, bridgeable and placeable tiles).</summary>
    public IReadOnlyList<OverlayTile> Highlights =>
        [.. Overlay.Tiles.Where(static tile => tile.Tint.StartsWith("--gv-overlay-", StringComparison.Ordinal))];

    /// <summary>The active lens's tile marks in the last overlay.</summary>
    public IReadOnlyList<OverlayTile> LensMarks =>
        [.. Overlay.Tiles.Where(static tile => tile.Tint.StartsWith("--gv-lens-", StringComparison.Ordinal))];

    /// <summary>The calls made to <paramref name="identifier"/>.</summary>
    /// <param name="identifier">The module export.</param>
    /// <returns>The recorded calls, oldest first.</returns>
    public IReadOnlyList<JSRuntimeInvocation> Calls(string identifier)
    {
        ThrowIfAnyClickFailed();
        return [.. Module.Invocations[identifier]];
    }

    /// <summary>Rethrows the failure of any click so far, so a handler that crashed after an await can't pass silently.</summary>
    public void ThrowIfAnyClickFailed()
    {
        if (_clicks.Select(static click => click.Exception).FirstOrDefault(static failure => failure is not null) is { } failure)
        {
            ExceptionDispatchInfo.Throw(failure.InnerException ?? failure);
        }
    }

    /// <summary>Clicks <paramref name="tile"/> as the renderer does: through the relay of the latest mount.</summary>
    /// <param name="tile">The clicked tile.</param>
    /// <returns>A task that completes when the click is handled.</returns>
    public Task ClickAsync(HexCoord tile)
    {
        var relay = Module.Invocations["mount"][^1].Arguments[1]!;
        var target = relay.GetType().GetProperty("Value")!.GetValue(relay)!;
        var click = target.GetType().GetMethod("OnTileClicked", BindingFlags.Public | BindingFlags.Instance)!;
        var handled = (Task)click.Invoke(target, [tile.Q, tile.R])!;
        _clicks.Add(handled);
        return handled;
    }

    /// <summary>Clicks <paramref name="tile"/> like a DOM click: started, not awaited; a failure so far is thrown.</summary>
    /// <param name="tile">The clicked tile.</param>
    public void Click(HexCoord tile)
    {
        _ = ClickAsync(tile);
        ThrowIfAnyClickFailed();
    }

    private T Last<T>(string identifier)
    {
        ThrowIfAnyClickFailed();
        return Module.Invocations[identifier].SelectMany(static call => call.Arguments).OfType<T>().Last();
    }
}
