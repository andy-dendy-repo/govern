using Govern.Domain.Entities;

namespace Govern.Application.Games.Models;

/// <summary>
/// A seat as shown to the players of a game: no email and no user id, only the per-game player id that also appears in the
/// <c>PlayerView</c>, so the client can tell which view player sits in which seat.
/// </summary>
/// <param name="PlayerId">The engine's id for this player (<c>PlayerView.Viewer</c>, unit and city owners).</param>
/// <param name="Seat">Zero-based seat, which is also the turn order.</param>
/// <param name="IsYou">Whether this is the caller's seat.</param>
/// <param name="IsCreator">Whether this player created the game.</param>
public sealed record GamePlayerSummary(Guid PlayerId, int Seat, bool IsYou, bool IsCreator)
{
    /// <summary>Maps a loaded seat for the given caller.</summary>
    /// <param name="player">The seat.</param>
    /// <param name="callerId">The caller's user id.</param>
    /// <param name="creatorId">The game creator's user id.</param>
    /// <returns>The summary.</returns>
    public static GamePlayerSummary From(GamePlayer player, Guid callerId, Guid creatorId)
    {
        ArgumentNullException.ThrowIfNull(player);
        return new GamePlayerSummary(player.Id, player.Seat, player.UserId == callerId, player.UserId == creatorId);
    }
}
