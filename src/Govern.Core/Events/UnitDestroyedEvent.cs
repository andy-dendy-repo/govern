using Govern.Core.Hexes;
using Govern.Core.Players;
using Govern.Core.Units;

namespace Govern.Core.Events;

/// <summary>
/// A unit was destroyed by an attack or removed because its owner left the game.
/// </summary>
/// <param name="UnitId">The destroyed unit.</param>
/// <param name="Owner">Its owner.</param>
/// <param name="Position">Where it stood.</param>
public sealed record UnitDestroyedEvent(UnitId UnitId, PlayerId Owner, HexCoord Position) : GameEvent;
