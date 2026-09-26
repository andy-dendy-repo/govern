namespace Govern.Infrastructure.Options;

/// <summary>
/// Database behaviour bound from the <c>Database</c> configuration section.
/// The connection string itself is <c>ConnectionStrings:Govern</c> (user-secrets locally, Key Vault in Azure).
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Database";

    /// <summary>The connection string name.</summary>
    public const string ConnectionStringName = "Govern";

    /// <summary>
    /// Applies pending EF Core migrations at startup (default <see langword="true"/>). Disable it where
    /// migrations are applied by a separate step.
    /// </summary>
    public bool MigrateOnStartup { get; set; } = true;
}
