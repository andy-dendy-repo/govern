using System.Globalization;
using Govern.Client.Features.Games.Lenses;
using Govern.Client.Features.Games.Models;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.Visibility;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// Builds the <see cref="TileCard"/> for the tile under the pointer (or the one just clicked): what the SVG board's titles used
/// to say, from the player's view alone, so it never tells more than the board shows.
/// </summary>
public static class TileCardBuilder
{
    /// <summary>Describes a tile.</summary>
    /// <param name="ruleset">The game's ruleset, for the names.</param>
    /// <param name="view">The player's view.</param>
    /// <param name="coord">The tile.</param>
    /// <param name="lens">The active lens, whose sentences for the tile and its edges end the card; <see langword="null"/> for none.</param>
    /// <returns>The card, or <see langword="null"/> for a tile off the map or not explored yet.</returns>
    public static TileCard? For(Ruleset ruleset, PlayerView view, HexCoord coord, IMapLens? lens)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(view);
        if (!view.Contains(coord) || view.TileAt(coord) is not { Terrain: { } terrain } tile)
        {
            return null;
        }

        var lines = new List<string>();
        if (view.CityAt(coord) is { } city)
        {
            lines.Add($"{Whose(view, city.Owner)} city: {city.Name}");
        }

        if (view.UnitAt(coord) is { } unit)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"{Whose(view, unit.Owner)} unit: {ruleset.UnitName(unit.Type)}, health {unit.Health} of {ruleset.Combat.MaxHealth}"));
        }

        AddFeatures(ruleset, view, coord, tile, lines);
        AddRivers(ruleset, view, coord, lines);
        if (tile.Visibility != TileVisibility.Visible)
        {
            lines.Add("Out of sight");
        }

        if (lens is not null)
        {
            AddLens(view, coord, lens, lines);
        }

        return new TileCard(coord, ruleset.TerrainName(terrain), lines);
    }

    private static string Whose(PlayerView view, Core.State.PlayerId owner) => owner == view.Viewer ? "Your" : "Enemy";

    // What stands on the tile (a building, the building one of the player's cities is producing there, or an improvement) and
    // whose territory it is in.
    private static void AddFeatures(Ruleset ruleset, PlayerView view, HexCoord coord, TileView tile, List<string> lines)
    {
        if (tile.Building is { } building)
        {
            lines.Add($"Building: {ruleset.BuildingName(building)}");
        }
        else if (view.Cities.FirstOrDefault(own => own.ProducingBuildingSite == coord) is { ProducingBuilding: { } planned })
        {
            lines.Add($"Building: {ruleset.BuildingName(planned)} (being built)");
        }

        if (tile.Improvement is { } improvement)
        {
            lines.Add($"Improvement: {ruleset.ImprovementName(improvement)}");
        }

        if (tile.TerritoryOwner is { } owner)
        {
            var name = tile.TerritoryCity is { } own ? view.Cities.FirstOrDefault(city => city.Id == own)?.Name : null;
            lines.Add(name is null ? $"{Whose(view, owner)} territory" : $"Territory of {name}");
        }
    }

    // Each kind of river on the tile's edges once, with its rules, and the bridges over them.
    private static void AddRivers(Ruleset ruleset, PlayerView view, HexCoord coord, List<string> lines)
    {
        lines.AddRange(view.RiversAround(coord).Distinct().Select(river => RiverDescription.For(ruleset, river)));
        var bridges = coord.Neighbors().Count(neighbor => view.BridgeBetween(coord, neighbor));
        if (bridges > 0)
        {
            lines.Add(bridges == 1 ? "Bridge over the river" : string.Create(CultureInfo.InvariantCulture, $"{bridges} bridges over the river"));
        }
    }

    // The lens's sentence for the tile, then those of the six edges around it (an edge's badge belongs to the tile that owns
    // the edge, which is this tile for three of them and a neighbour for the others).
    private static void AddLens(PlayerView view, HexCoord coord, IMapLens lens, List<string> lines)
    {
        var index = (coord.Row * view.Width) + coord.Column;
        if (lens.MarkFor(view, index) is { } mark)
        {
            lines.Add(mark.Description);
        }

        var edges = lens.EdgeMarksFor(view.Tiles[index]).Concat(coord.Neighbors()
            .Where(view.Contains)
            .SelectMany(neighbor => lens.EdgeMarksFor(view.TileAt(neighbor)).Where(edge => new HexEdge(neighbor, edge.Side).Neighbor() == coord)));
        lines.AddRange(edges.Select(static edge => edge.Description).Distinct());
    }
}
