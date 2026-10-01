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
public sealed record CityForecast(
    ImmutableArray<ResourceAmount> Income,
    UpkeepResult Upkeep,
    ImmutableArray<ResourceAmount> MissingToBegin,
    int ProductionPerTurn);
