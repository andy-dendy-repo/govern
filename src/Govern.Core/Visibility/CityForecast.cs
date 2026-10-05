using System.Collections.Immutable;
using Govern.Core.Economy;

namespace Govern.Core.Visibility;

/// <summary>What one of the viewer's cities will do with its stock at the end of their turn, if nothing else changes.</summary>
/// <param name="Income">What it adds first (<see cref="Engine.GameEngine.CityIncome"/>).</param>
/// <param name="Upkeep">What it then pays for its buildings and the units on its territory, and what it can't pay.</param>
/// <param name="MissingToBegin">
/// What it lacks to pay the resource cost of what it produces, after its upkeep; empty when it is paid already or can be.
/// Assumes the city produces this turn: a city whose production goes to a unit's job pays nothing and gains nothing.
/// </param>
/// <param name="ProductionPerTurn">
/// The production its item gains: that of its buildings still working after the upkeep, or 0 while the item can't begin.
/// Like <paramref name="MissingToBegin"/>, it assumes the city's production doesn't go to a job.
/// </param>
/// <param name="Storage">
/// Its storage caps when the stock is cut (<see cref="Cities.CityStorage.Caps"/>): the buildings still working after the upkeep,
/// plus the building its production finishes this turn. Like <paramref name="MissingToBegin"/> it assumes the city's production
/// doesn't go to a job, and it ignores a builder's upgrade finished this turn.
/// </param>
/// <param name="Lost">
/// What won't fit in <paramref name="Storage"/> once it has paid its upkeep, its item's cost and its next tile (when it holds its
/// price), and is lost; empty when all fits. Like <paramref name="MissingToBegin"/> it assumes the city's production doesn't go
/// to a job (then the item's cost stays in the stock, which the client adds back), ignores trade, and assumes a tile is left to
/// claim.
/// </param>
/// <param name="Sources">Where <paramref name="ProductionPerTurn"/> comes from, when the item can begin (<see cref="Cities.CityProduction.Sources"/>).</param>
public sealed record CityForecast(
    ImmutableArray<ResourceAmount> Income,
    UpkeepResult Upkeep,
    ImmutableArray<ResourceAmount> MissingToBegin,
    int ProductionPerTurn,
    ImmutableArray<ResourceAmount> Storage,
    ImmutableArray<ResourceAmount> Lost,
    ImmutableArray<Cities.ProductionSource> Sources);
