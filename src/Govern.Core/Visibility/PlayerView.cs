using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Visibility;

/// <summary>
/// The game as one player may see it: the only shape sent to a client. It hides unexplored terrain and every enemy unit or city
/// that is not on a currently visible tile, so reading it in the browser reveals nothing the player could not see on screen.
/// </summary>
/// <param name="Viewer">Player this view is for.</param>
/// <param name="Version">
/// Opaque fingerprint of this view (a hash of everything else in it): equal views have equal versions. Compare it only for
/// equality (the client echoes it as <c>expectedVersion</c>); it is not ordered and does not count commands, so it reveals
/// nothing the viewer cannot already see.
/// </param>
/// <param name="Turn">Round number.</param>
/// <param name="CurrentPlayer">Player who may act now.</param>
/// <param name="Status">Whether the game is still running.</param>
/// <param name="Winner">The winner once finished; otherwise <see langword="null"/>.</param>
/// <param name="Width">Map columns.</param>
/// <param name="Height">Map rows.</param>
/// <param name="Tiles">Every tile, row-major, with what the viewer knows about it.</param>
/// <param name="Units">The viewer's units and the enemy units on visible tiles, ordered by id.</param>
/// <param name="Cities">The viewer's cities and the enemy cities on visible tiles, ordered by id.</param>
/// <param name="Players">Participants in turn order.</param>
public sealed record PlayerView(
    PlayerId Viewer,
    long Version,
    int Turn,
    PlayerId CurrentPlayer,
    GameStatus Status,
    PlayerId? Winner,
    int Width,
    int Height,
    ImmutableArray<TileView> Tiles,
    ImmutableArray<Unit> Units,
    ImmutableArray<City> Cities,
    ImmutableArray<PlayerSummary> Players);
