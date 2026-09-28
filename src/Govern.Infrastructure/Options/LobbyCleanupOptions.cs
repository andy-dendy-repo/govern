using System.ComponentModel.DataAnnotations;

namespace Govern.Infrastructure.Options;

/// <summary>
/// Settings for the background sweep that deletes expired lobbies, bound from the <c>LobbyCleanup</c> section. How long a
/// lobby lives is a game rule (<c>Game.LobbyLifetime</c>), not configuration.
/// </summary>
public sealed class LobbyCleanupOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "LobbyCleanup";

    /// <summary>
    /// Time between sweeps (default 1 hour); the first sweep runs one interval after startup. An expired lobby therefore
    /// lingers for up to one interval.
    /// </summary>
    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// How many lobbies one delete statement removes (default 500); a sweep repeats until nothing is left.
    /// </summary>
    [Range(1, 10_000)]
    public int BatchSize { get; set; } = 500;
}
