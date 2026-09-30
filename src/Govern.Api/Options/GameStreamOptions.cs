using System.ComponentModel.DataAnnotations;

namespace Govern.Api.Options;

/// <summary>
/// Settings for the live game stream (<c>GET /api/games/{id}/stream</c>), bound from the <c>GameStream</c> section.
/// </summary>
public sealed class GameStreamOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "GameStream";

    /// <summary>
    /// How long the stream may stay silent before a <c>heartbeat</c> event is sent (default 20 seconds). Keeps proxies and
    /// Azure's load balancer (idle timeout about 230 seconds) from closing the connection and lets clients detect a dead one.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00.100", "00:01:00")]
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(20);
}
