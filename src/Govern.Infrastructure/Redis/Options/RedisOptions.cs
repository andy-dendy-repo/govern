using System.ComponentModel.DataAnnotations;

namespace Govern.Infrastructure.Redis.Options;

/// <summary>
/// Redis settings bound from the <c>Redis</c> configuration section. Redis is optional: it is used only when the
/// <c>ConnectionStrings:Redis</c> connection string is set (user-secrets locally, Key Vault in Azure), and then carries the
/// game change backplane, the shared rate limits and the shared sign-in throttle between Api instances.
/// </summary>
public sealed class RedisOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Redis";

    /// <summary>The connection string name.</summary>
    public const string ConnectionStringName = "Redis";

    /// <summary>
    /// Prefix of every key and channel the Api uses (default <c>govern</c>), so several environments can share one Redis
    /// database.
    /// </summary>
    [Required]
    [RegularExpression("^[a-z0-9-]{1,32}$")]
    public string KeyPrefix { get; set; } = "govern";

    /// <summary>Builds a key or channel name under <see cref="KeyPrefix"/>.</summary>
    /// <param name="name">The name after the prefix, e.g. <c>game-changed</c>.</param>
    /// <returns><c>&lt;prefix&gt;:&lt;name&gt;</c>.</returns>
    public string Key(string name) => $"{KeyPrefix}:{name}";
}
