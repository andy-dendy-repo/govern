using Govern.Core.Economy;

namespace Govern.Core.Cities;

/// <summary>Where some of a city's production per turn comes from (<see cref="CityProduction.Sources"/>).</summary>
/// <param name="Building">The working building that gives it; <see langword="null"/> for the city's own production.</param>
/// <param name="Amount">Production points per turn.</param>
public sealed record ProductionSource(BuildingType? Building, int Amount);
