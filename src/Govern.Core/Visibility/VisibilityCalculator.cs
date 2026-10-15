using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
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
    /// plus the <see cref="TerrainDefinition.SightBonus"/> of the tile it stands on, and every tile of their cities' territory.
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
        var ownCities = state.Cities.Where(city => city.Owner == player).Select(static city => city.Id).ToHashSet();
        var citySight = state.Cities
            .Where(city => city.Owner == player)
            .SelectMany(city => city.Position.WithinRadius(ruleset.Cities.Sight + SightBonus(ruleset, state, city.Position)));
        var territory = state.Map.Tiles
            .Where(tile => tile.Territory is { } city && ownCities.Contains(city))
            .Select(static tile => tile.Coord);

        return unitSight.Concat(citySight).Where(state.Map.Contains).Concat(territory).ToHashSet();
    }

    /// <summary>
    /// Adds every currently visible tile, and every neighbour of their cities' territory, to each active player's
    /// <see cref="Player.Explored"/> list. The neighbours store some of the rivers on the territory's outer edges, so knowing
    /// their (unchanging) terrain and rivers lets the view compute a city's income exactly.
    /// </summary>
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
            var visible = VisibleTiles(ruleset, state, player.Id);
            var discovered = visible
                .Concat(TerritoryRing(state, player.Id))
                .Distinct()
                .Where(coord => !known.Contains(coord))
                .OrderBy(static coord => coord.Row)
                .ThenBy(static coord => coord.Column);
            return player with { Explored = [.. player.Explored, .. discovered], KnownPosts = KnownPosts(ruleset, state, player, visible) };
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
        var owners = state.Cities.ToDictionary(static city => city.Id, static city => city.Owner);
        var tiles = state.Map.Tiles.Select(tile => ToView(tile, visible, explored, owners, viewer));

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
                .Select(city => CityView.From(city, viewer, SeenTradePost(ruleset, state, city, viewer, visible)))],
            [.. state.Players.Select(static p => new PlayerSummary(p.Id, p.Status))],
            TradeDeals(state, viewer),
            new ScienceView(ScienceRule.Income(ruleset, state, viewer), player.Nodes),
            TradePartners(ruleset, state, player));

        return view with { Version = Fingerprint(view) };
    }

    /// <summary>
    /// The events of an accepted command that one player may see, in order: everything about their own units and cities, the
    /// turn, resignations and the result, and another player's events only on tiles they could see when the command began or
    /// after it ended (a move is cut to those tiles; an attacker on an unseen tile loses its id and tile). Another player's
    /// production, jobs and upkeep never pass. Hosts store or send only these, never <see cref="GameUpdate.Events"/>.
    /// </summary>
    /// <param name="ruleset">The game's ruleset.</param>
    /// <param name="before">The state the command was applied to.</param>
    /// <param name="update">The command's outcome.</param>
    /// <param name="viewer">A participant (active or not).</param>
    /// <returns>The events the player may see; empty when they saw nothing.</returns>
    public static ImmutableArray<GameEvent> EventsFor(Ruleset ruleset, GameState before, GameUpdate update, PlayerId viewer)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(update);

        return EventVisibilityCalculator.EventsFor(ruleset, before, update, viewer);
    }

    // The player's known trade posts plus the other players' cities whose trade post and city tile they both see now (a post
    // seen alone doesn't say which city owns it), ids in the order seen (new ones by id); null while there are none.
    private static ImmutableArray<CityId>? KnownPosts(Ruleset ruleset, GameState state, Player player, IReadOnlySet<HexCoord> visible)
    {
        var known = player.KnownPostList;
        var seen = state.Cities
            .Where(city => city.Owner != player.Id && !known.Contains(city.Id))
            .Where(city => visible.Contains(city.Position) && TradeRule.PostTile(ruleset, state.Map, city.Id) is { } post && visible.Contains(post))
            .OrderBy(static city => city.Id.Value)
            .Select(static city => city.Id)
            .ToList();
        return seen.Count == 0 ? player.KnownPosts : [.. known, .. seen];
    }

    // A city's trade post tile when the viewer may see it: always their own, an enemy's while its tile is in sight.
    private static HexCoord? SeenTradePost(Ruleset ruleset, GameState state, City city, PlayerId viewer, IReadOnlySet<HexCoord> visible) =>
        TradeRule.PostTile(ruleset, state.Map, city.Id) is { } post && (city.Owner == viewer || visible.Contains(post)) ? post : null;

    // The deals the viewer is a side of, ordered by (random) id; null when there are none.
    private static ImmutableArray<TradeDealView>? TradeDeals(GameState state, PlayerId viewer)
    {
        var deals = state.TradeDealList
            .Select(deal => TradeDealView.From(state, deal, viewer))
            .OfType<TradeDealView>()
            .OrderBy(static deal => deal.Id.Value)
            .ToImmutableArray();
        return deals.IsEmpty ? null : deals;
    }

    // Other active players' cities with a trade post the viewer may trade with, ordered by id: those whose post they have seen
    // (Player.KnownPosts, with the post's tile), and the other side of each deal they are a side of (without it, unless seen:
    // an offer from an unseen city tells its name, not where it is). Each market has what its agreed deals the viewer is not a
    // side of use taken off (the client takes off the viewer's own); null when none.
    private static ImmutableArray<TradePartnerView>? TradePartners(Ruleset ruleset, GameState state, Player viewer)
    {
        var viewersDeals = state.TradeDealList.Where(deal => deal.Owner == viewer.Id || deal.HostOwner == viewer.Id).ToList();
        var dealIds = viewersDeals.Select(static deal => deal.Id).ToHashSet();
        var known = viewer.KnownPostList.ToHashSet();
        var partners = known
            .Concat(viewersDeals.Select(deal => deal.Owner == viewer.Id ? deal.Host : deal.Home))
            .Distinct()
            .Select(state.FindCity)
            .OfType<City>()
            .Where(city => city.Owner != viewer.Id && state.FindPlayer(city.Owner) is { Status: PlayerStatus.Active }
                && TradeRule.PostTile(ruleset, state.Map, city.Id) is not null)
            .OrderBy(static city => city.Id.Value)
            .Select(city => PartnerOf(ruleset, state, city, known.Contains(city.Id), dealIds))
            .ToImmutableArray();
        return partners.IsEmpty ? null : partners;
    }

    private static TradePartnerView PartnerOf(Ruleset ruleset, GameState state, City city, bool seen, IReadOnlySet<TradeDealId> viewersDeals)
    {
        var market = TradeRule.FreeMarket(state, city, viewersDeals);
        var post = seen ? TradeRule.PostTile(ruleset, state.Map, city.Id) : null;
        return new TradePartnerView(city.Id, city.Owner, city.Name, post, market.IsEmpty ? null : market, city.DemandList.IsEmpty ? null : city.DemandList);
    }

    // The first 8 bytes of the view's SHA-256 (non-negative). It depends only on what the viewer sees, so changes hidden from
    // them leave it unchanged, and it cannot be counted or ordered to learn how often the opponent acted.
    private static long Fingerprint(PlayerView view)
    {
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(view, GameSerializer.Options));
        return BinaryPrimitives.ReadInt64LittleEndian(hash) & long.MaxValue;
    }

    // The tiles next to the player's territory that aren't in it.
    private static IEnumerable<HexCoord> TerritoryRing(GameState state, PlayerId player)
    {
        var ownCities = state.Cities.Where(city => city.Owner == player).Select(static city => city.Id).ToHashSet();
        return state.Map.Tiles
            .Where(tile => tile.Territory is { } city && ownCities.Contains(city))
            .SelectMany(static tile => tile.Coord.Neighbors())
            .Where(state.Map.Contains);
    }

    private static int SightBonus(Ruleset ruleset, GameState state, HexCoord position) =>
        ruleset.Terrain(state.Map.TileAt(position).Terrain).SightBonus;

    private static TileView ToView(
        Tile tile,
        IReadOnlySet<HexCoord> visible,
        HashSet<HexCoord> explored,
        Dictionary<CityId, PlayerId> owners,
        PlayerId viewer) => tile.Coord switch
        {
            var coord when visible.Contains(coord) => new TileView(
                TileVisibility.Visible,
                tile.Terrain,
                tile.Rivers,
                tile.Bridges,
                tile.Improvement,
                tile.Territory is { } city ? owners[city] : null,
                tile.Territory is { } own && owners[own] == viewer ? own : null,
                tile.Building),
            var coord when explored.Contains(coord) => new TileView(TileVisibility.Explored, tile.Terrain, tile.Rivers),
            _ => new TileView(TileVisibility.Unexplored, null),
        };
}
