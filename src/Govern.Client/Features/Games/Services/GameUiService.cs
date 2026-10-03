using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Lenses;
using Govern.Client.Features.Games.Models;
using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.Visibility;

namespace Govern.Client.Features.Games.Services;

/// <summary>
/// The UI state of one game screen: what is selected, which tiles are highlighted for it, and which map lens is on.
/// <c>GameTable</c> creates one per game and passes it down as a cascading value, so the panels read the selection instead of
/// receiving it through parameter chains. It also holds the screen's own UI state (the collapsed selection card, the log
/// drawer, browser full screen); later mechanics add theirs here (an open large view).
/// It holds no game state: the view stays the server's, and every method takes the view it applies to.
/// </summary>
public sealed class GameUiService
{
    /// <summary>The selected unit or city.</summary>
    public BoardSelection Selection { get; private set; } = BoardSelection.None;

    /// <summary>The tiles marked for the selection.</summary>
    public BoardHighlights Highlights { get; private set; } = BoardHighlights.None;

    /// <summary>The map lens drawn over the board, or <see langword="null"/> when none is on.</summary>
    public IMapLens? ActiveLens { get; private set; }

    /// <summary>
    /// The index of the open tab of the player's city panel (0 Units, 1 Buildings, 2 Economy). It stays open when another city
    /// is selected, so the player can go through their cities on the same tab.
    /// </summary>
    public int CityTab { get; set; }

    /// <summary>Whether the game log drawer is open (closed until the player opens it with the top bar's Log button).</summary>
    public bool LogOpen { get; private set; }

    /// <summary>The newest log entry's <see cref="GameLogItem.Sequence"/> when the drawer was last open (or the screen opened).</summary>
    public int LogSeen { get; private set; }

    /// <summary>Whether the selection card shows only its heading; selecting something else opens it again.</summary>
    public bool SelectionCollapsed { get; private set; }

    /// <summary>Whether the game screen fills the whole display (the browser's full screen mode).</summary>
    public bool FullScreen { get; private set; }

    /// <summary>The latest request to centre the board on a tile, or <see langword="null"/> before the first.</summary>
    public BoardFocus? Focus { get; private set; }

    /// <summary>Raised after the state changed.</summary>
    public event Action? Changed;

    /// <summary>Selects a unit, a city or nothing, and recomputes the highlights.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The view on screen.</param>
    /// <param name="selection">The new selection.</param>
    public void Select(Ruleset ruleset, PlayerView view, BoardSelection selection)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(selection);
        SelectionCollapsed &= SameThing(Selection, selection);
        Selection = selection;
        Highlights = BoardClickResolver.Highlights(ruleset, view, selection);
        Changed?.Invoke();
    }

    /// <summary>
    /// Shows a tile from the game log: centres the board on it and selects the player's own unit, or else city, standing there.
    /// </summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The view on screen.</param>
    /// <param name="tile">The tile.</param>
    public void Show(Ruleset ruleset, PlayerView view, HexCoord tile)
    {
        ArgumentNullException.ThrowIfNull(view);
        Focus = new BoardFocus(tile, (Focus?.Request ?? 0) + 1);
        var selection = view.Units.FirstOrDefault(unit => unit.Position == tile && unit.Owner == view.Viewer) is { } unit
            ? BoardSelection.Of(unit.Id)
            : view.Cities.FirstOrDefault(city => city.Position == tile && city.Owner == view.Viewer) is { } city
                ? BoardSelection.Of(city.Id)
                : BoardSelection.None;
        Select(ruleset, view, selection);
    }

    /// <summary>Keeps the state valid for a new view: drops a selection that is no longer in it and recomputes the highlights.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The new view.</param>
    public void Refresh(Ruleset ruleset, PlayerView view) => Select(ruleset, view, BoardClickResolver.Prune(ruleset, view, Selection));

    /// <summary>Drops the pending attack target and keeps the unit selected (the forecast's Cancel).</summary>
    public void CancelAttack()
    {
        if (Selection.Target is null)
        {
            return;
        }

        Selection = Selection with { Target = null };
        Changed?.Invoke();
    }

    /// <summary>Turns bridge mode on for the selected unit, or off when it is on (the action bar's Build bridge).</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The view on screen.</param>
    public void ToggleBridge(Ruleset ruleset, PlayerView view)
    {
        if (Selection.Unit is null)
        {
            return;
        }

        Select(ruleset, view, Selection with { Target = null, Destination = false, Bridge = !Selection.Bridge });
    }

    /// <summary>
    /// Starts choosing a destination for the selected unit (the action bar's Go to), or stops when it is choosing one.
    /// </summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The view on screen.</param>
    public void ToggleDestination(Ruleset ruleset, PlayerView view)
    {
        if (Selection.Unit is null)
        {
            return;
        }

        Select(ruleset, view, Selection with { Target = null, Bridge = false, Destination = !Selection.Destination });
    }

    /// <summary>
    /// Starts choosing a tile for a building of the selected city (its panel's Build button), or stops when it is the building
    /// already being placed; the board marks the tiles that can take it.
    /// </summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The view on screen.</param>
    /// <param name="building">The building.</param>
    public void PlaceBuilding(Ruleset ruleset, PlayerView view, BuildingType building)
    {
        if (Selection.City is null)
        {
            return;
        }

        Select(ruleset, view, Selection with { Placing = Selection.Placing == building ? null : building });
    }

    /// <summary>
    /// The player's units that can still act this turn: movement left, no job running and no destination to walk to, in id order (the top bar's
    /// "units can still move").
    /// </summary>
    /// <param name="view">The view on screen.</param>
    /// <returns>The units; empty when none can move.</returns>
    public static IReadOnlyList<UnitView> IdleUnits(PlayerView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return [.. view.Units
            .Where(unit => unit.Owner == view.Viewer && unit.MovementLeft > 0 && unit.Job is null && unit.Destination is null)
            .OrderBy(static unit => unit.Id.Value)];
    }

    /// <summary>Selects the next of <see cref="IdleUnits"/> after the selected unit (the first when none is), wrapping round.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The view on screen.</param>
    public void SelectNextIdleUnit(Ruleset ruleset, PlayerView view)
    {
        var idle = IdleUnits(view);
        if (idle.Count == 0)
        {
            return;
        }

        var next = idle.FirstOrDefault(unit => Selection.Unit is { } selected && unit.Id.Value > selected.Value) ?? idle[0];
        Select(ruleset, view, BoardSelection.Of(next.Id));
    }

    /// <summary>Handles Escape: closes the log drawer, else cancels a pending attack, bridge mode, choosing a destination or placing a building, else clears the selection.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The view on screen.</param>
    /// <returns>Whether anything changed.</returns>
    public bool Escape(Ruleset ruleset, PlayerView view)
    {
        if (LogOpen)
        {
            LogOpen = false;
            Changed?.Invoke();
            return true;
        }

        if (Selection.Bridge || Selection.Destination || Selection.Placing is not null)
        {
            Select(ruleset, view, Selection with { Bridge = false, Destination = false, Placing = null });
            return true;
        }

        if (Selection.Target is not null)
        {
            CancelAttack();
            return true;
        }

        if (Selection == BoardSelection.None)
        {
            return false;
        }

        (Selection, Highlights) = (BoardSelection.None, BoardHighlights.None);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Turns a lens on, or off when it is the active one. One lens is on at a time.</summary>
    /// <param name="lens">The lens the player toggled.</param>
    public void ToggleLens(IMapLens lens)
    {
        ArgumentNullException.ThrowIfNull(lens);
        ActiveLens = ActiveLens?.Id == lens.Id ? null : lens;
        Changed?.Invoke();
    }

    /// <summary>Opens the log drawer, or closes it when it is open; opening marks the log as seen.</summary>
    /// <param name="log">The player's log on screen.</param>
    public void ToggleLog(IReadOnlyList<GameLogItem>? log)
    {
        LogOpen = !LogOpen;
        MarkLogSeen(log);
        Changed?.Invoke();
    }

    /// <summary>Marks every entry of the log as seen, so <see cref="UnreadLog"/> counts only later ones.</summary>
    /// <param name="log">The player's log on screen.</param>
    public void MarkLogSeen(IReadOnlyList<GameLogItem>? log) => LogSeen = Math.Max(LogSeen, Newest(log));

    /// <summary>How many log entries arrived since the drawer was last open; 0 while it is open.</summary>
    /// <param name="log">The player's log on screen.</param>
    /// <returns>The number of entries.</returns>
    public int UnreadLog(IReadOnlyList<GameLogItem>? log) =>
        LogOpen ? 0 : (log ?? []).Count(item => item.Sequence > LogSeen);

    /// <summary>Shows only the selection card's heading, or the whole card again.</summary>
    public void ToggleSelectionCollapsed()
    {
        SelectionCollapsed = !SelectionCollapsed;
        Changed?.Invoke();
    }

    /// <summary>Records whether the browser shows the game screen in full screen (it can also leave on its own, by Esc).</summary>
    /// <param name="fullScreen">Whether the screen is in full screen now.</param>
    public void SetFullScreen(bool fullScreen)
    {
        if (FullScreen == fullScreen)
        {
            return;
        }

        FullScreen = fullScreen;
        Changed?.Invoke();
    }

    private static int Newest(IReadOnlyList<GameLogItem>? log) => log is { Count: > 0 } ? log.Max(static item => item.Sequence) : 0;

    // The same unit or city, whatever mode it is in (a pending attack, bridge mode, placing a building).
    private static bool SameThing(BoardSelection current, BoardSelection next) =>
        current.Unit == next.Unit && current.City == next.City;
}
