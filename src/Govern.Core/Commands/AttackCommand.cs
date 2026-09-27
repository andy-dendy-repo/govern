using Govern.Core.Hexes;
using Govern.Core.Units;

namespace Govern.Core.Commands;

/// <summary>
/// Attacks an adjacent tile: destroys the enemy unit on it, or captures the enemy city when no unit defends it.
/// </summary>
/// <param name="UnitId">The actor's attacking unit.</param>
/// <param name="Target">Adjacent tile with an enemy unit or city.</param>
public sealed record AttackCommand(UnitId UnitId, HexCoord Target) : GameCommand;
