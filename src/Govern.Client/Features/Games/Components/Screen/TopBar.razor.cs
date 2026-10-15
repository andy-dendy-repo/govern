using Govern.Client.Helpers;
using Govern.Core.State;

namespace Govern.Client.Features.Games.Components.Screen;

/// <summary>The top strip's texts about the players: whose turn it is and what resigning does.</summary>
public partial class TopBar
{
    // Only a player still in the game ends turns and resigns; one who left watches until it ends.
    private bool IsActive => View.Players.Any(player => player.Id == View.Viewer && player.Status == PlayerStatus.Active);

    private string ResignQuestion => View.Players.Count(static player => player.Status == PlayerStatus.Active) > 2
        ? "Resign the game? It goes on for the others."
        : "Resign the game? Your opponent wins.";

    private string TurnText => View.Status == GameStatus.Finished
        ? $"Turn {View.Turn}: game over"
        : $"Turn {View.Turn}: {WhoseTurn}";

    // With two players the other one is "the opponent"; with more they are told apart by seat.
    private string WhoseTurn
    {
        get
        {
            if (View.CurrentPlayer == View.Viewer)
            {
                return "your turn";
            }

            return View.Players.Length > 2 ? $"seat {Seats.Number(View, View.CurrentPlayer)}'s turn" : "opponent's turn";
        }
    }
}
