using System.Text.Json.Serialization;
using Govern.Core.Economy;
using Govern.Core.Map;

namespace Govern.Core.Units;

/// <summary>
/// What a unit is building: an improvement on its own tile, or a bridge over the river between its tile and a neighbour. At the
/// end of its owner's turn the job gains the unit's <see cref="UnitDefinition.BuildProduction"/>, plus its city's production
/// inside that city's territory; it is done when <see cref="Progress"/> reaches the cost. Moving, attacking or starting another
/// job drops it with its progress.
/// </summary>
/// <param name="Progress">Production points put into the job so far.</param>
/// <param name="StartedTurn">
/// The round the job started in. A city feeds one job per turn, the oldest (then the lowest unit id).
/// </param>
/// <param name="Improvement">The improvement being built on the unit's tile; <see langword="null"/> for a bridge.</param>
/// <param name="BridgeTo">The neighbour across the river being bridged; <see langword="null"/> for an improvement.</param>
public sealed record UnitJob(
    int Progress,
    int StartedTurn,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ImprovementType? Improvement = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] HexCoord? BridgeTo = null)
{
    /// <summary>A job building an improvement on the unit's tile.</summary>
    /// <param name="improvement">The improvement.</param>
    /// <param name="turn">The current round.</param>
    /// <returns>The new job, with no progress.</returns>
    public static UnitJob ForImprovement(ImprovementType improvement, int turn) => new(0, turn, Improvement: improvement);

    /// <summary>A job bridging the river between the unit's tile and a neighbour.</summary>
    /// <param name="target">The neighbour across the river.</param>
    /// <param name="turn">The current round.</param>
    /// <returns>The new job, with no progress.</returns>
    public static UnitJob ForBridge(HexCoord target, int turn) => new(0, turn, BridgeTo: target);
}
