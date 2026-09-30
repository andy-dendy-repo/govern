using Govern.Client.Features.Games.Enums;

namespace Govern.Client.Features.Games.Models;

/// <summary>
/// A change in a watched game: its connection status, and the latest snapshot when one arrived.
/// </summary>
/// <param name="Status">The connection status.</param>
/// <param name="Game">The new snapshot; <see langword="null"/> when only the status changed.</param>
/// <param name="RetryIn">With <see cref="GameWatchStatus.Reconnecting"/>, the delay before the next attempt.</param>
public sealed record GameWatchUpdate(GameWatchStatus Status, GetGameResponse? Game = null, TimeSpan? RetryIn = null);
