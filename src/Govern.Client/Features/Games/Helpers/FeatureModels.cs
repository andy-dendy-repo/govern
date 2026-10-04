using Govern.Client.Features.Games.Enums;
using Govern.Core.Economy;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>
/// The 3D board's model per ruleset improvement and building. Presentation only, so it lives in the client; every ruleset
/// improvement and building needs an entry (<c>FeatureModelsTests</c>). An unknown improvement is drawn as a field and an unknown
/// building as a hall, so a new id still shows.
/// </summary>
public static class FeatureModels
{
    /// <summary>The model of an improvement that has no entry.</summary>
    public const FeatureModel DefaultImprovement = FeatureModel.Field;

    /// <summary>The model of a building that has no entry.</summary>
    public const FeatureModel DefaultBuilding = FeatureModel.Hall;

    private static readonly Dictionary<string, FeatureModel> ImprovementsById = new(StringComparer.Ordinal)
    {
        ["farm"] = FeatureModel.Field,
        ["lumber_camp"] = FeatureModel.Camp,
        ["mine"] = FeatureModel.Mine,
    };

    private static readonly Dictionary<string, FeatureModel> BuildingsById = new(StringComparer.Ordinal)
    {
        ["army_camp"] = FeatureModel.Tents,
        ["campus"] = FeatureModel.Dome,
        ["trade_post"] = FeatureModel.Stall,
        ["culture_centre"] = FeatureModel.Hall,
        ["harbour"] = FeatureModel.Pier,
        ["workshop"] = FeatureModel.Hall,
        ["barracks"] = FeatureModel.Tents,
        ["university"] = FeatureModel.Dome,
        ["market"] = FeatureModel.Stall,
        ["port"] = FeatureModel.Pier,
    };

    /// <summary>The improvement ids that have an entry.</summary>
    public static IEnumerable<string> KnownImprovements => ImprovementsById.Keys;

    /// <summary>The building ids that have an entry.</summary>
    public static IEnumerable<string> KnownBuildings => BuildingsById.Keys;

    /// <summary>The model of an improvement.</summary>
    /// <param name="improvement">The improvement.</param>
    /// <returns>Its model, or <see cref="DefaultImprovement"/> when it has no entry.</returns>
    public static FeatureModel For(ImprovementType improvement) => ImprovementsById.GetValueOrDefault(improvement.Id, DefaultImprovement);

    /// <summary>The model of a building.</summary>
    /// <param name="building">The building.</param>
    /// <returns>Its model, or <see cref="DefaultBuilding"/> when it has no entry.</returns>
    public static FeatureModel For(BuildingType building) => BuildingsById.GetValueOrDefault(building.Id, DefaultBuilding);
}
