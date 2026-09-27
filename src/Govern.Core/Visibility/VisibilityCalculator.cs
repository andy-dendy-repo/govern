using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Hexes;
using Govern.Core.Maps;
using Govern.Core.Players;
using Govern.Core.State;

namespace Govern.Core.Visibility;

/// <summary>
/// Fog of war: which tiles a player sees now, what they have explored, and the filtered <see cref="PlayerView"/>.
/// </summary>
public static class VisibilityCalculator
{
    /// <summary>Tiles currently in sight of the player's units (their type's sight) and cities (<see cref="City.Sight"/>).</summary>
    /// <param name="state">The game.</param>
    /// <param name="player">The player.</param>
    /// <returns>Visible tiles on the map.</returns>
    public static IReadOnlySet<HexCoord> VisibleTiles(GameState state, PlayerId player)
    {
        ArgumentNullException.ThrowIfNull(state);

        var unitSight = state.Units
            .Where(unit => unit.Owner == player)
            .SelectMany(static unit => unit.Position.WithinRadius(unit.Definition.Sight));
        var citySight = state.Cities
            .Where(city => city.Owner == player)
            .SelectMany(static city => city.Position.WithinRadius(City.Sight));

        return unitSight.Concat(citySight).Where(state.Map.Contains).ToHashSet();
    }

    /// <summary>Adds every currently visible tile to each active player's <see cref="Player.Explored"/> list.</summary>
    /// <param name="state">The game.</param>
    /// <returns>The state with updated exploration; newly seen tiles are appended in row-then-column order.</returns>
    public static GameState UpdateExplored(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var players = state.Players.Select(player =>
        {
            if (player.Status != PlayerStatus.Active)
            {
                return player;
            }

            var known = player.Explored.ToHashSet();
            var discovered = VisibleTiles(state, player.Id)
                .Where(coord => !known.Contains(coord))
                .OrderBy(static coord => coord.Row)
                .ThenBy(static coord => coord.Column);
            return player with { Explored = [.. player.Explored, .. discovered] };
        });

        return state with { Players = [.. players] };
    }

    /// <summary>Builds what one participant may see of the game.</summary>
    /// <param name="state">The game.</param>
    /// <param name="viewer">A participant (active or not).</param>
    /// <returns>The filtered view.</returns>
    /// <exception cref="ArgumentException"><paramref name="viewer"/> is not part of the game.</exception>
    public static PlayerView ViewFor(GameState state, PlayerId viewer)
    {
        ArgumentNullException.ThrowIfNull(state);
        var player = state.FindPlayer(viewer) ?? throw new ArgumentException("The viewer is not part of the game.", nameof(viewer));

        var visible = VisibleTiles(state, viewer);
        var explored = player.Explored.ToHashSet();
        var tiles = state.Map.Tiles.Select(tile => ToView(tile, visible, explored));

        return new PlayerView(
            viewer,
            state.Version,
            state.Turn,
            state.CurrentPlayer,
            state.Status,
            state.Winner,
            state.Map.Width,
            state.Map.Height,
            [.. tiles],
            [.. state.Units.Where(unit => unit.Owner == viewer || visible.Contains(unit.Position))],
            [.. state.Cities.Where(city => city.Owner == viewer || visible.Contains(city.Position))],
            [.. state.Players.Select(static p => new PlayerSummary(p.Id, p.Status))]);
    }

    private static TileView ToView(Tile tile, IReadOnlySet<HexCoord> visible, HashSet<HexCoord> explored) => tile.Coord switch
    {
        var coord when visible.Contains(coord) => new TileView(coord, TileVisibility.Visible, tile.Terrain),
        var coord when explored.Contains(coord) => new TileView(coord, TileVisibility.Explored, tile.Terrain),
        var coord => new TileView(coord, TileVisibility.Unexplored, null),
    };
}
