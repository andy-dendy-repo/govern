using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;
using Govern.Core.Visibility;

namespace Govern.Client.Features.Design.Helpers;

/// <summary>
/// The view the map preview (<c>/design/maps</c>) draws for a pool map: every tile in sight with its terrain and rivers, and a
/// first starting unit on each spawn candidate (the first player's in the west, their mirrors for the second player), so an
/// author sees where games can begin.
/// </summary>
public static class MapPreviewBuilder
{
    /// <summary>The first player, who sees the view.</summary>
    public static readonly PlayerId You = new(Guid.Parse("0199b3c4-0000-7000-8000-00000000001a"));

    /// <summary>The second player.</summary>
    public static readonly PlayerId Them = new(Guid.Parse("0199b3c4-0000-7000-8000-00000000001b"));

    /// <summary>Builds the preview of a map.</summary>
    /// <param name="ruleset">The ruleset the map was validated against (its first starting unit stands on the spawns).</param>
    /// <param name="map">The pool map.</param>
    /// <param name="version">The view's version; a new map needs a new one, so the board redraws.</param>
    /// <returns>The view.</returns>
    public static PlayerView Build(Ruleset ruleset, PredefinedMap map, long version)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(map);
        var type = ruleset.Setup.StartingUnits[0];
        var health = ruleset.Combat.MaxHealth;
        var spawns = map.Spawns.Select(spawn => (Owner: You, Position: spawn))
            .Concat(map.Spawns.Select(spawn => (Owner: Them, Position: map.Map.MirrorOf(spawn))));
        var units = spawns.Select((spawn, index) =>
            new UnitView(new UnitId(index + 1), spawn.Owner, type, spawn.Position, spawn.Owner == You ? 0 : null, health));
        var tiles = map.Map.Tiles.Select(static tile => new TileView(TileVisibility.Visible, tile.Terrain, tile.Rivers));

        return new PlayerView(
            You, version, 1, You, GameStatus.InProgress, null, map.Map.Width, map.Map.Height, [.. tiles], [.. units], [],
            [new PlayerSummary(You, PlayerStatus.Active), new PlayerSummary(Them, PlayerStatus.Active)]);
    }
}
