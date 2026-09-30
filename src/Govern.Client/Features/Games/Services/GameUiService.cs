using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Models;
using Govern.Core.Rulesets;
using Govern.Core.Visibility;

namespace Govern.Client.Features.Games.Services;

/// <summary>
/// The UI state of one game screen: what is selected and which tiles are highlighted for it. <c>GameTable</c> creates one
/// per game and passes it down as a cascading value, so the panels read the selection instead of receiving it through
/// parameter chains. Later mechanics add their UI state here (the active lens, an open full-screen view).
/// It holds no game state: the view stays the server's, and every method takes the view it applies to.
/// </summary>
public sealed class GameUiService
{
    /// <summary>The selected unit or city.</summary>
    public BoardSelection Selection { get; private set; } = BoardSelection.None;

    /// <summary>The tiles marked for the selection.</summary>
    public BoardHighlights Highlights { get; private set; } = BoardHighlights.None;

    /// <summary>Raised after the state changed.</summary>
    public event Action? Changed;

    /// <summary>Selects a unit, a city or nothing, and recomputes the highlights.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The view on screen.</param>
    /// <param name="selection">The new selection.</param>
    public void Select(Ruleset ruleset, PlayerView view, BoardSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        Selection = selection;
        Highlights = BoardClickResolver.Highlights(ruleset, view, selection);
        Changed?.Invoke();
    }

    /// <summary>Keeps the state valid for a new view: drops a selection that is no longer in it and recomputes the highlights.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="view">The new view.</param>
    public void Refresh(Ruleset ruleset, PlayerView view) => Select(ruleset, view, BoardClickResolver.Prune(view, Selection));

    /// <summary>Handles Escape: clears the selection.</summary>
    /// <returns>Whether anything changed.</returns>
    public bool Escape()
    {
        if (Selection == BoardSelection.None)
        {
            return false;
        }

        (Selection, Highlights) = (BoardSelection.None, BoardHighlights.None);
        Changed?.Invoke();
        return true;
    }
}
