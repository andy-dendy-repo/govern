using Govern.Core.State;
using Govern.Core.Visibility;

namespace Govern.Client.Helpers;

/// <summary>
/// A player's seat: their place in the turn order (<see cref="PlayerView.Players"/>), which picks their colour everywhere
/// (<c>--gv-seat-1</c>, <c>--gv-seat-2</c>… in <c>wwwroot/css/tokens.css</c>).
/// </summary>
public static class Seats
{
    /// <summary>The seat number of a player, from 1.</summary>
    /// <param name="view">The view that lists the players.</param>
    /// <param name="player">The player.</param>
    /// <returns>The seat number, or 0 when the player is not in the view.</returns>
    public static int Number(PlayerView view, PlayerId player)
    {
        ArgumentNullException.ThrowIfNull(view);
        for (var index = 0; index < view.Players.Length; index++)
        {
            if (view.Players[index].Id == player)
            {
                return index + 1;
            }
        }

        return 0;
    }

    /// <summary>The CSS class that colours a player's pieces and icons.</summary>
    /// <param name="view">The view that lists the players.</param>
    /// <param name="player">The player.</param>
    /// <returns><c>seat-</c> followed by the seat number.</returns>
    public static string Class(PlayerView view, PlayerId player) => $"seat-{Number(view, player)}";
}
