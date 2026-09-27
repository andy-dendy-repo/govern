using System.Collections.Immutable;
using Govern.Core.Events;
using Govern.Core.State;

namespace Govern.Core.Engine;

/// <summary>
/// The outcome of an accepted command: the new state and what happened, in order.
/// </summary>
/// <param name="State">The state after the command; its <see cref="GameState.Version"/> is one higher.</param>
/// <param name="Events">Events in the order they happened.</param>
public sealed record GameUpdate(GameState State, ImmutableArray<GameEvent> Events);
