using Govern.Client.Features.Games.Enums;
using Govern.Core.Units;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// The 3D board's model per ruleset unit. Presentation only, so it lives in the client; every ruleset unit needs an entry
/// (<c>PieceModelsTests</c>). An unknown unit is drawn as a soldier, so a new id still shows; its banner shows its own icon.
/// </summary>
public static class PieceModels
{
    /// <summary>The model of a unit that has no entry.</summary>
    public const PieceModel DefaultUnit = PieceModel.Soldier;

    private static readonly Dictionary<string, PieceModel> UnitsById = new(StringComparer.Ordinal)
    {
        ["settler"] = PieceModel.Settler,
        ["builder"] = PieceModel.Builder,
        ["soldier"] = PieceModel.Soldier,
        ["spearman"] = PieceModel.Spearman,
        ["horseman"] = PieceModel.Horseman,
        ["archer"] = PieceModel.Archer,
        ["scout"] = PieceModel.Scout,
        ["trader"] = PieceModel.Trader,
    };

    /// <summary>The unit ids that have an entry.</summary>
    public static IEnumerable<string> KnownUnits => UnitsById.Keys;

    /// <summary>The model of a unit.</summary>
    /// <param name="unit">The unit type.</param>
    /// <returns>Its model, or <see cref="DefaultUnit"/> when it has no entry.</returns>
    public static PieceModel For(UnitType unit) => UnitsById.GetValueOrDefault(unit.Id, DefaultUnit);
}
