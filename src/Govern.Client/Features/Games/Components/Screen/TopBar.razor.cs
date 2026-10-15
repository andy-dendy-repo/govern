using Govern.Client.Helpers;
using Govern.Core.State;

namespace Govern.Client.Features.Games.Components.Screen;

/// <summary>
/// The top strip's texts about the players: whose turn it is (in a simultaneous game, who has ended the round) and what
/// resigning does.
/// </summary>
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

    // With two players the other one is "the opponent"; with more they are told apart by seat. In a simultaneous game everyone
    // plays the round, so the text says who has ended it.
    private string WhoseTurn
    {
        get
        {
            if (View.TurnMode == TurnMode.Simultaneous)
            {
                return RoundText;
            }

            if (View.CurrentPlayer == View.Viewer)
            {
                return "your turn";
            }

            return View.Players.Length > 2 ? $"seat {Seats.Number(View, View.CurrentPlayer)}'s turn" : "opponent's turn";
        }
    }

    private string RoundText
    {
        get
        {
            var others = View.Players.Where(player => player.Id != View.Viewer && player.Status == PlayerStatus.Active).ToList();
            var ended = others.Count(static player => player.EndedTurn);
            if (View.EndedTurn)
            {
                var waiting = others.Count - ended;
                return others.Count == 1 ? "waiting for your opponent" : $"waiting for {waiting} {(waiting == 1 ? "player" : "players")}";
            }

            if (!IsActive || ended == 0)
            {
                return "everyone plays";
            }

            return others.Count == 1 ? "your opponent has ended" : $"{ended} of {others.Count} others have ended";
        }
    }
}
