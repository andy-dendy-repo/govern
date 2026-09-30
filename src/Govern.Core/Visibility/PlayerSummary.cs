using Govern.Core.State;

namespace Govern.Core.Visibility;

/// <summary>
/// Public information about a participant (without what they have explored).
/// </summary>
/// <param name="Id">Player identifier.</param>
/// <param name="Status">Whether the player still takes part.</param>
public sealed record PlayerSummary(PlayerId Id, PlayerStatus Status);
