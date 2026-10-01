using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Govern.Core.Cities;
using Govern.Core.Economy;

namespace Govern.Core.Visibility;

/// <summary>Whether one of the viewer's units will get its upkeep at the end of their turn, if nothing else changes.</summary>
/// <param name="City">The city whose territory it stands on and which keeps it up; <see langword="null"/> when it lives off the land.</param>
/// <param name="Shortfall">What it will lack (the city's unpaid upkeep, or what the land doesn't give); empty when supplied.</param>
public sealed record UnitSupply(CityId? City, ImmutableArray<ResourceAmount> Shortfall)
{
    /// <summary>Whether the unit gets everything it needs.</summary>
    [JsonIgnore]
    public bool Supplied => Shortfall.IsEmpty;
}
