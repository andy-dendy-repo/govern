namespace Govern.Core.Engine;

/// <summary>
/// Every rule violation the engine returns. Codes are stable; never rename one silently.
/// </summary>
public static class GameErrors
{
    /// <summary>The game has already finished.</summary>
    public static readonly GameError GameFinished = new("game.finished", "The game has finished.");

    /// <summary>The actor is not an active player of this game.</summary>
    public static readonly GameError NotAPlayer = new("game.not_a_player", "You are not an active player in this game.");

    /// <summary>It is another player's turn.</summary>
    public static readonly GameError NotYourTurn = new("game.not_your_turn", "It is not your turn.");

    /// <summary>The actor has no unit with that id (it does not exist or belongs to someone else; the two are not distinguished).</summary>
    public static readonly GameError UnitNotFound = new("unit.not_found", "You have no such unit.");

    /// <summary>The unit has no movement points left this turn.</summary>
    public static readonly GameError NoMovementLeft = new("unit.no_movement", "The unit has no movement left this turn.");

    /// <summary>The unit type cannot found cities.</summary>
    public static readonly GameError CannotFoundCity = new("unit.cannot_found_city", "Only settlers can found cities.");

    /// <summary>The unit type cannot attack.</summary>
    public static readonly GameError CannotAttack = new("unit.cannot_attack", "This unit cannot attack.");

    /// <summary>A visible unit stands on the target tile (hidden units make the tile <see cref="Unreachable"/> instead).</summary>
    public static readonly GameError TileOccupied = new("move.occupied", "Another unit stands on that tile.");

    /// <summary>The target tile is off the map, the unit's own tile, blocked or too far for the unit's remaining movement.</summary>
    public static readonly GameError Unreachable = new("move.unreachable", "The unit cannot reach that tile this turn.");

    /// <summary>The tile already holds a city.</summary>
    public static readonly GameError CityTileTaken = new("city.tile_taken", "A city already stands on this tile.");

    /// <summary>Another city is closer than the minimum spacing.</summary>
    public static readonly GameError CityTooClose = new("city.too_close", "Another city is too close.");

    /// <summary>The actor has no city with that id (it does not exist or belongs to someone else; the two are not distinguished).</summary>
    public static readonly GameError CityNotFound = new("city.not_found", "You have no such city.");

    /// <summary>The unit type is not defined.</summary>
    public static readonly GameError UnknownUnitType = new("city.unknown_unit_type", "That unit type does not exist.");

    /// <summary>The attack target is not adjacent to the attacker.</summary>
    public static readonly GameError NotAdjacent = new("attack.not_adjacent", "The target is not next to the unit.");

    /// <summary>There is no enemy unit or city on the target tile.</summary>
    public static readonly GameError NoTarget = new("attack.no_target", "There is nothing to attack on that tile.");

    /// <summary>The target belongs to the attacker.</summary>
    public static readonly GameError OwnTarget = new("attack.own_target", "You cannot attack your own unit or city.");
}
