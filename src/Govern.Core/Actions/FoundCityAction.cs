using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Actions;

/// <summary>
/// Founds a city on the settler's tile; the settler is consumed.
/// </summary>
/// <param name="UnitId">The actor's settler.</param>
/// <param name="Name">
/// The new city's name, chosen by the player: trimmed, inner spaces collapsed, <see cref="MaxNameLength"/> characters at
/// most, letters, digits, spaces and <c>' - .</c> only, and not the name of another of the actor's cities (ignoring case).
/// </param>
public sealed record FoundCityCommand(UnitId UnitId, string Name) : GameCommand
{
    /// <summary>
    /// The longest city name, in characters. An input limit (it keeps names readable on the board and in panels), not a game
    /// rule, so it is not in the ruleset.
    /// </summary>
    public const int MaxNameLength = 24;
}

/// <summary>
/// Turns a settler into a city on its tile, at least the ruleset's <see cref="CityRules.MinimumSpacing"/> away from every other
/// city and outside every city's territory. The city takes the player's name for it, builds its initial production, starts
/// with <see cref="CityRules.StartingStock"/> and claims the unowned tiles within <see cref="TerritoryRules.InitialRadius"/>.
/// </summary>
internal static class FoundCityRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, FoundCityCommand command)
    {
        if (!CommandGuardRule.TryGetOwnedUnit(state, actor, command.UnitId, out var settler, out var error))
        {
            return error;
        }

        var tileOwned = state.Map.TileAt(settler.Position).Territory is not null;
        if (Check(ruleset, settler, state.Cities.Select(static city => city.Position), tileOwned) is { } violation)
        {
            return violation;
        }

        if (CheckName(command.Name, state.Cities.Where(city => city.Owner == actor).Select(static city => city.Name)) is { } nameViolation)
        {
            return nameViolation;
        }

        var (allocated, id) = state.AllocateId();
        var name = NormaliseName(command.Name);
        var stock = YieldCalculator.Sum(ruleset, ruleset.Cities.StartingStock);
        var city = new City(new CityId(id), actor, name, settler.Position, ruleset.Cities.InitialProduction, ProductionProgress: 0, Stock: stock.IsEmpty ? null : stock);
        var founded = allocated.WithoutUnit(settler.Id);
        founded = founded with { Cities = founded.Cities.Add(city), Map = TerritoryRule.ClaimAround(ruleset, founded.Map, city) };

        return new GameUpdate(founded, [new CityFoundedEvent(city.Id, city.Owner, city.Position, city.Name, settler.Id)]);
    }

    // Shared with the view-based preview (ViewPreviewCalculator). Every city closer than the spacing is visible to the settler
    // (settler sight >= spacing - 1, checked by RulesetValidator), and so is the territory of its own tile, so the view gives the
    // same answer as the state.
    public static RuleViolation? Check(Ruleset ruleset, Unit settler, IEnumerable<HexCoord> cityPositions, bool tileOwned)
    {
        if (!ruleset.Unit(settler.Type).CanFoundCity)
        {
            return RuleViolations.CannotFoundCity;
        }

        if (settler.MovementLeft <= 0)
        {
            return RuleViolations.NoMovementLeft;
        }

        var positions = cityPositions.ToList();
        if (positions.Contains(settler.Position))
        {
            return RuleViolations.CityTileTaken;
        }

        if (positions.Any(position => position.DistanceTo(settler.Position) < ruleset.Cities.MinimumSpacing))
        {
            return RuleViolations.CityTooClose;
        }

        return tileOwned ? RuleViolations.CityInTerritory : null;
    }

    // Shared with the view-based preview and the ruleset validator. Only the actor's own cities count as taken, so a rejection
    // never tells them the name of a city they can't see.
    public static RuleViolation? CheckName(string? name, IEnumerable<string> ownCityNames)
    {
        if (name is null)
        {
            return RuleViolations.CityNameInvalid;
        }

        var normalised = NormaliseName(name);
        if (normalised.Length is 0 or > FoundCityCommand.MaxNameLength || !normalised.All(IsAllowed))
        {
            return RuleViolations.CityNameInvalid;
        }

        return ownCityNames.Any(own => string.Equals(own, normalised, StringComparison.OrdinalIgnoreCase))
            ? RuleViolations.CityNameTaken
            : null;
    }

    // Trims the name and collapses every run of whitespace inside it into one space.
    public static string NormaliseName(string name) =>
        string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static bool IsAllowed(char character) =>
        char.IsLetterOrDigit(character) || character is ' ' or '\'' or '-' or '.';
}
