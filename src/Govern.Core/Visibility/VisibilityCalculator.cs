using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.Serialization;
using Govern.Core.State;

namespace Govern.Core.Visibility;

/// <summary>
/// Fog of war: which tiles a player sees now, what they have explored, and the filtered <see cref="PlayerView"/>.
/// </summary>
public static class VisibilityCalculator
{
    /// <summary>
    /// Tiles currently in sight of the player's units (their type's sight) and cities (<see cref="CityRules.Sight"/>), each
    /// plus the <see cref="TerrainDefinition.SightBonus"/> of the tile it stands on.
    /// </summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="state">The game.</param>
    /// <param name="player">The player.</param>
    /// <returns>Visible tiles on the map.</returns>
    public static IReadOnlySet<HexCoord> VisibleTiles(Ruleset ruleset, GameState state, PlayerId player)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(state);

        var unitSight = state.Units
            .Where(unit => unit.Owner == player)
            .SelectMany(unit => unit.Position.WithinRadius(ruleset.Unit(unit.Type).Sight + SightBonus(ruleset, state, unit.Position)));
        var citySight = state.Cities
            .Where(city => city.Owner == player)
            .SelectMany(city => city.Position.WithinRadius(ruleset.Cities.Sight + SightBonus(ruleset, state, city.Position)));

        return unitSight.Concat(citySight).Where(state.Map.Contains).ToHashSet();
    }

    /// <summary>Adds every currently visible tile to each active player's <see cref="Player.Explored"/> list.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="state">The game.</param>
    /// <returns>The state with updated exploration; newly seen tiles are appended in row-then-column order.</returns>
    public static GameState UpdateExplored(Ruleset ruleset, GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var players = state.Players.Select(player =>
        {
            if (player.Status != PlayerStatus.Active)
            {
                return player;
            }

            var known = player.Explored.ToHashSet();
            var discovered = VisibleTiles(ruleset, state, player.Id)
                .Where(coord => !known.Contains(coord))
                .OrderBy(static coord => coord.Row)
                .ThenBy(static coord => coord.Column);
            return player with { Explored = [.. player.Explored, .. discovered] };
        });

        return state with { Players = [.. players] };
    }

    /// <summary>Builds what one participant may see of the game.</summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="state">The game.</param>
    /// <param name="viewer">A participant (active or not).</param>
    /// <returns>The filtered view; its <see cref="PlayerView.Version"/> is a fingerprint of the rest of the view.</returns>
    /// <exception cref="ArgumentException"><paramref name="viewer"/> is not part of the game.</exception>
    public static PlayerView ViewFor(Ruleset ruleset, GameState state, PlayerId viewer)
    {
        ArgumentNullException.ThrowIfNull(state);
        var player = state.FindPlayer(viewer) ?? throw new ArgumentException("The viewer is not part of the game.", nameof(viewer));

        var visible = VisibleTiles(ruleset, state, viewer);
        var explored = player.Explored.ToHashSet();
        var tiles = state.Map.Tiles.Select(tile => ToView(tile, visible, explored));

        var view = new PlayerView(
            viewer,
            Version: 0,
            state.Turn,
            state.CurrentPlayer,
            state.Status,
            state.Winner,
            state.Map.Width,
            state.Map.Height,
            [.. tiles],
            // Ordered by (random) id, not by creation, so the order reveals nothing about when enemy objects were created.
            [.. state.Units
                .Where(unit => unit.Owner == viewer || visible.Contains(unit.Position))
                .OrderBy(static unit => unit.Id.Value)
                .Select(unit => UnitView.From(ruleset, unit, viewer))],
            [.. state.Cities
                .Where(city => city.Owner == viewer || visible.Contains(city.Position))
                .OrderBy(static city => city.Id.Value)
                .Select(city => CityView.From(city, viewer))],
            [.. state.Players.Select(static p => new PlayerSummary(p.Id, p.Status))],
            player.Stockpile);

        return view with { Version = Fingerprint(view) };
    }

    // The first 8 bytes of the view's SHA-256 (non-negative). It depends only on what the viewer sees, so changes hidden from
    // them leave it unchanged, and it cannot be counted or ordered to learn how often the opponent acted.
    private static long Fingerprint(PlayerView view)
    {
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(view, GameSerializer.Options));
        return BinaryPrimitives.ReadInt64LittleEndian(hash) & long.MaxValue;
    }

    private static int SightBonus(Ruleset ruleset, GameState state, HexCoord position) =>
        ruleset.Terrain(state.Map.TileAt(position).Terrain).SightBonus;

    private static TileView ToView(Tile tile, IReadOnlySet<HexCoord> visible, HashSet<HexCoord> explored) => tile.Coord switch
    {
        var coord when visible.Contains(coord) => new TileView(TileVisibility.Visible, tile.Terrain, tile.Rivers, tile.Bridges, tile.Improvement),
        var coord when explored.Contains(coord) => new TileView(TileVisibility.Explored, tile.Terrain, tile.Rivers),
        _ => new TileView(TileVisibility.Unexplored, null),
    };
}
