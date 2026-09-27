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
    /// Assemblies loaded into <see cref="Instance"/>: every layered project, including the Blazor client (ignored projects are not layered).
    /// </summary>
    public static readonly string[] AssemblyNames = [.. Layers.AllowedReferences.Keys];

    /// <summary>
    /// Regex matching every namespace that belongs to Govern production code.
    /// </summary>
    public const string GovernNamespace = @"^Govern(?:\..*)?$";

    /// <summary>
    /// The loaded architecture.
    /// </summary>
    public static Architecture Instance { get; } = new ArchLoader()
        .LoadAssemblies(AssemblyNames.Select(static name => System.Reflection.Assembly.Load(new System.Reflection.AssemblyName(name)))
            .Concat(FrameworkAssemblies)
            .ToArray())
        .Build();

    // Framework base types that Govern types derive from indirectly (LayoutComponentBase : ComponentBase); loading them lets
    // assignability rules follow the hierarchy. Rules still only check types in Govern namespaces.
    private static System.Reflection.Assembly[] FrameworkAssemblies => [typeof(Microsoft.AspNetCore.Components.ComponentBase).Assembly];
}
