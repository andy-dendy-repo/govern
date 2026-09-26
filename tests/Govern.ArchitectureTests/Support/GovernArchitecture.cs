using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using Govern.ArchitectureTests.Conventions;

namespace Govern.ArchitectureTests.Support;

/// <summary>
/// The ArchUnitNET model of all Govern production assemblies, loaded once per test run.
/// </summary>
public static class GovernArchitecture
{
    /// <summary>
    /// Assemblies loaded into <see cref="Instance"/> (every layered project except the Blazor client and ignored ones).
    /// </summary>
    public static readonly string[] AssemblyNames = Layers.AllowedReferences.Keys
        .Where(static name => name != "Govern.Client")
        .ToArray();

    /// <summary>
    /// Regex matching every namespace that belongs to Govern production code.
    /// </summary>
    public const string GovernNamespace = @"^Govern(?:\..*)?$";

    /// <summary>
    /// The loaded architecture.
    /// </summary>
    public static Architecture Instance { get; } = new ArchLoader()
        .LoadAssemblies(AssemblyNames.Select(static name => System.Reflection.Assembly.Load(new System.Reflection.AssemblyName(name))).ToArray())
        .Build();
}
