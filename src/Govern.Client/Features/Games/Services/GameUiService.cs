using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Lenses;
using Govern.Client.Features.Games.Models;
using Govern.Core.Rulesets;
using Govern.Core.Visibility;

namespace Govern.Client.Features.Games.Services;

/// <summary>
/// The UI state of one game screen: what is selected, which tiles are highlighted for it, and which map lens is on.
/// <c>GameTable</c> creates one per game and passes it down as a cascading value, so the panels read the selection instead of
/// receiving it through parameter chains. Later mechanics add their UI state here (an open full-screen view).
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
        Selection = selection;
        Highlights = BoardClickResolver.Highlights(ruleset, view, selection);
        Changed?.Invoke();
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

        Select(ruleset, view, Selection with { Target = null, Bridge = !Selection.Bridge });
    }

    /// <summary>Handles Escape: cancels a pending attack or bridge mode, otherwise clears the selection.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The view on screen.</param>
    /// <returns>Whether anything changed.</returns>
    public bool Escape(Ruleset ruleset, PlayerView view)
    {
        if (Selection.Bridge)
        {
            Select(ruleset, view, Selection with { Bridge = false });
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
}
